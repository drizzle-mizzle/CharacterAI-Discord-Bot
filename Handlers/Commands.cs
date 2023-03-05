using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;

namespace CharacterAI_Discord_Bot.Handlers
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandsHandler _handler;

        public Commands(CommandsHandler handler)
        {
            _handler = handler;
        }

        [Command("find character")]
        [Alias("find")]
        public async Task FindCharacter([Remainder] string query)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                using (Context.Message.Channel.EnterTypingState())
                    _ = FindCharacterAsync(query, _handler, Context);
        }

        [Command("set character")]
        [Alias("sc", "set")]
        public async Task SetCharacter(string charID)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                using (Context.Message.Channel.EnterTypingState())
                    _ = SetCharacterAsync(charID, _handler, Context);
        }

        [Command("reset character")]
        [Alias("reset")]
        public async Task ResetCharacter()
        {
            if (Context.Guild is not null && !ValidateBotRole(Context)) 
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else if (_handler.CurrentIntegration.CurrentCharacter.IsEmpty)
                await Context.Message.ReplyAsync("⚠ Set a character first").ConfigureAwait(false);
            else
                using (Context.Message.Channel.EnterTypingState())
                    _ = ResetCharacterAsync(_handler, Context);
        }

        [Command("private")]
        [Alias("pc")]
        public async Task CreatePrivateChat()
        {
            if (BotConfig.PrivateChatRoleRequired && !ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else if (_handler.CurrentIntegration.CurrentCharacter.IsEmpty)
                await Context.Message.ReplyAsync("⚠ Set a character first").ConfigureAwait(false);
            else
                _ = CreatePrivateChatAsync(_handler, Context);
        }

        [Command("add")]
        public async Task AddUser(ulong channelId, SocketGuildUser user)
        {
            var currentChannel = _handler.Channels.Find(c => c.Id == channelId && c.AuthorId == Context.User.Id);
            if (currentChannel is null) return;

            var showChannel = new OverwritePermissions(viewChannel: PermValue.Allow);
            var discordChannel = Context.Guild.GetChannel(channelId);
            await discordChannel.AddPermissionOverwriteAsync(user, showChannel);

            currentChannel.GuestsList.Add(user.Id);

            SaveData(channels: _handler.Channels);
        }

        [Command("kick")]
        public async Task KickUser(SocketGuildUser user)
        {
            var currentChannel = _handler.Channels.Find(c => c.Id == Context.Channel.Id && c.AuthorId == Context.User.Id);
            if (currentChannel is null)
            {
                await Context.Message.ReplyAsync("Either you are not the creator of this chat, or it was deactivated.");
                return;
            }
            var hideChannel = new OverwritePermissions(viewChannel: PermValue.Deny);
            var discordChannel = Context.Guild.GetChannel(Context.Channel.Id);
            await discordChannel.AddPermissionOverwriteAsync(user, hideChannel);

            currentChannel.GuestsList.Remove(user.Id);

            SaveData(channels: _handler.Channels);
        }

        [Command("clear")]
        public async Task ClearPrivates()
        {
            if (!ValidateBotRole(Context))
            {
                await NoPermissionAlert(Context).ConfigureAwait(false);
                return;
            }

            var category = Context.Guild.CategoryChannels.FirstOrDefault(c => c.Name == BotConfig.Category);
            if (category is null) return;

            foreach (var channel in category.Channels.Cast<SocketTextChannel>())
            {
                if (_handler.Channels.Find(c => c.Id == channel.Id) is null)
                    _ = channel.DeleteAsync();
            }
        }

        [Command("audience toggle")]
        [Summary("Enable/disable audience mode")]
        [Alias("amode")]
        public async Task AudienceToggle(int mode)
        {
            if (Context.Guild is not null && !ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else 
            {
                var currentChannel = _handler.Channels.Find(c => c.Id == Context.Channel.Id);
                if (currentChannel is null) return;

                currentChannel.Data.AudienceMode = mode;
                string msgMode = mode == 3 ? "quote and username" : mode == 2 ? "quote only" : mode == 1 ? "username only" : "disabled";
                string text = "⚠ Audience mode: " + msgMode + '!';

                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
                SaveData(channels: _handler.Channels);
            }
        }

        [Command("call user")]
        [Summary("Make character call other user")]
        [Alias("call", "cu")]
        public async Task CallUser(SocketGuildUser user, string msg = "Hey!")
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await Context.Channel.SendMessageAsync(user.Mention + " " + msg).ConfigureAwait(false);
        }

        [Command("skip")]
        [Summary("Make character ignore next few messages")]
        [Alias("delay")]
        public async Task StopTalk(int amount = 3)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                var currentChannel = _handler.Channels.Find(c => c.Id == Context.Channel.Id);
                if (currentChannel is null) return;

                currentChannel.Data.SkipMessages = amount;
                await Context.Message.ReplyAsync($"⚠ Next {amount} message(s) will be ignored").ConfigureAwait(false);
            }
        }

        [Command("reply chance")]
        [Summary("Change the probability of random replies (%)")]
        [Alias("rc")]
        public async Task ChangeChance(int chance)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                string text = $"⚠ Probability of random replies was changed from {_handler.ReplyChance}% to {chance}%";
                _handler.ReplyChance = chance;
                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
        }

        [Command("hunt")]
        [Summary("Reply on each message of a certain user")]
        public async Task Hunt(SocketGuildUser user)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.HuntedUsers.Add(user.Id, 100);
                await Context.Message.ReplyAsync($"👻 Hunting {user.Mention}!").ConfigureAwait(false);
            }
        }

        [Command("unhunt")]
        public async Task Ununt(SocketGuildUser user)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.HuntedUsers.Remove(user.Id);
                await Context.Message.ReplyAsync($"{user.Mention} is not hunted anymore 👻").ConfigureAwait(false);
            }
        }

        [Command("hunt chance")]
        [Summary("Change the probability of replies to a hunted user (%)")]
        [Alias("hc")]
        public async Task HuntChance(SocketGuildUser user, int chance)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                string text = $"⚠ Probability of replies for {user.Mention} was changed from {_handler.HuntedUsers[user.Id]}% to {chance}%";
                _handler.HuntedUsers[user.Id] = chance;

                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
        }

        [Command("ignore")]
        [Alias("ban")]
        [Summary("Prevent user from calling the bot.")]
        public async Task Ignore(SocketGuildUser user)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.BlackList.Add(user.Id);
                SaveData(blackList: _handler.BlackList);
                await Context.Message.ReplyAsync($"⚠ {user.Mention} was added to the blacklist!").ConfigureAwait(false);
            }
        }

        [Command("allow")]
        [Alias("unban")]
        [Summary("Allow user to call the bot.")]
        public async Task Allow(SocketGuildUser user)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.BlackList.Remove(user.Id);
                SaveData(blackList: _handler.BlackList);
                await Context.Message.ReplyAsync($"⚠ {user.Mention} was removed from the blacklist!");
            }
        }

        [Command("activity")]
        public async Task UpdateStatus(string status, int type = 0)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await UpdatePlayingStatus(Context.Client, status: status, type: type);
        }

        [Command("status")]
        public async Task UpdateStatus(int status)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await Context.Client.SetStatusAsync((UserStatus)status);
        }

        [Command("link")]
        public async Task ShowLink()
        {
            var charInfo = _handler.CurrentIntegration.CurrentCharacter;
            string link = $"https://beta.character.ai/chat?char={charInfo.Id}";

            await Context.Message.ReplyAsync($"Chat with **{charInfo.Name}**:\n{link}").ConfigureAwait(false);
        }

        [Command("help")]
        public async Task ShowHelp(int page = 1)
        {
            var userCommands = new string[]
            {
                "`link` – Get original character link",
                "`ping` – check latency",
                "`add <channel_id> <user>` – add user to your private chat",
                "`kick <user>` – kick user from your private chat *(use it **in** channel you want to kick user from)*",
                "`private` – create private chat with a character *(every time user without bot role (or not server owner) creates new chat, his previous chat becomes inactive)*\n    Alias: `pc`"
            };
            var managerCommands = new string[]
            {
                "`find character <query>` – find and set character by it's name\n    Alias: `find`",
                "`set character <id>` – set character by id\n    Aliases: `set`, `sc`",
                "`reset character` – start new chat with a character *(for current text channel only, other channels won't be affected)*\n    Alias: `reset`",
                "`clear` – delete all inactive private channels",
                "`audience toggle <mode>` – enable/disable audience mode *(What is the audience mode - read below)*\n    Alias: `amode`\n    Mode: `0` – disabled, `1` – username only, `2` – quote only, `3` – quote and username",
                "`call user <user> <any text>` – Make character call other user *(you can use it to make two bots talk to each other)*\n    Aliases: `call`, `cu`\n    Example: `@some_character call @another_character Do you love donuts?`\n    *(if no text argument provided, default `\"Hey!\"` will be used)*",
                "`skip <amount>` – Make character ignore next few messages *(use it to stop bots' conversation)*\n    Alias: `delay`\n    *(if no amount argument provided, default `3` will be used)*\n    *(commands will not be ignored, amount can be reduced with another call)*",
                "`reply chance <chance>` – Change the probability of random replies on new users' messages `in %` *(It's better to use it with audience mode enabled)*\n    Alias: `rc`\n    Example: `rc 50` => `Probability of random answers was changed from 0% to 50%`\n    *(default value = 0%)*\n    *(keep in mind that with this feature enabled, commands can be executed without bot prefix/mention)*",
                "`hunt <@user_mention>` – Make character always reply on messages of a certain user",
                "`unhunt <@user_mention>` – Stop hunting user",
                "`hunt chance <@user_mention> <chance>` – Change the probability of replies to a hunted user `in %`\n    Alias: `hc`\n    Example: `hc @user 50` => `Probability of replies for @user was changed from 100% to 50%`\n    *(default value = 100%)*",
                "`ignore <@user_mention>` – Prevent user from calling the bot\n    Alias: `ban`",
                "`allow <@user_mention>` – Allow user to call the bot\n    Alias: `unban`",
                "`activity <text> <type>` – change bot activity status\n    Type: `0` – Playing, `1` – Streaming, `2` – Listening, `3` – Watching, `4` – Custom (not working), `5` – Competing\n    *(default `type` value = 0)*\n    *(provide `0` for `text` to clear activity)*",
                "`status <type>` – change bot presence status\n    Type: `0` – Offline, `1` – Online, `2` – Idle, `3` – AFK, `4` – DND, `5` – Invisible"
            };
            if (Context.Guild is null)
                await Context.Message.ReplyAsync($"{userCommands[0]}\n{userCommands[1]}\n{managerCommands[2]}\n{managerCommands[4]}");
            else if (!ValidateBotRole(Context))
                await Context.Message.ReplyAsync(string.Join(", ", userCommands));
            else
            {
                var pages = Math.Ceiling((double)(managerCommands.Length / 5)) + 1;
                string text;

                if (page == 1)
                    text = $"**Page 1/{pages}:**\n" + string.Join("\n", userCommands);
                else // 2: 0..4, 3: 5..9, 4: 10..14
                {
                    int pos = page * 5 - 10;
                    text = $"**Page {page}/{pages}:**\n" + string.Join("\n", managerCommands[pos..(pos + 5)]);
                }
                await Context.Message.ReplyAsync(text);
            }
        }

        [Command("ping")]
        public async Task Ping()
            => await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms").ConfigureAwait(false);
    }
}

