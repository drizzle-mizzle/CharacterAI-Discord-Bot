using Discord;
using Discord.Commands;
using static CharacterAI_Discord_Bot.Service.CommandsService;

namespace CharacterAI_Discord_Bot.Handlers
{
    public class NewCommands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandHandler _handler;

        public NewCommands(CommandHandler handler)
        {
            _handler = handler;
        }

        [Command("link")]
        public async Task ShowId()
        {
            var charInfo = _handler.integration.charInfo;
            string link = $"https://beta.character.ai/chat?char={charInfo.CharId}";

            await Context.Message.ReplyAsync($"Chat with **{charInfo.Name}**:\n{link}");
        }

        [Command("set character")]
        [Alias("sc", "set")]
        public async Task SetCharacter(string charID)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
                await Task.Run(() => SetCharacterAsync(charID, _handler, Context).ConfigureAwait(false));
        }

        [Command("reset character")]
        [Alias("reset")]
        public async Task ResetCharacter()
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
                await Task.Run(() => ResetCharacterAsync(_handler, Context).ConfigureAwait(false));
        }

        [Command("audience toggle")]
        [Summary("Enable/disable audience mode")]
        [Alias("amode")]
        public async Task AudienceToggle()
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else 
            {
                _handler.integration.audienceMode ^= true;
                await UpdatePlayingStatus(_handler.integration, Context.Client).ConfigureAwait(false);
                await Context.Message.ReplyAsync("⚠ Audience mode " + (_handler.integration.audienceMode ? "enabled" : "disabled") + '!');
            }
        }

        [Command("call user")]
        [Summary("Make character call other user")]
        [Alias("call", "cu")]
        public async Task CallUser(IUser user, string msg = "Hey!")
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
                await Context.Channel.SendMessageAsync(user.Mention + " " + msg);
        }

        [Command("skip")]
        [Summary("Make character ignore next few messages")]
        [Alias("delay")]
        public async Task StopTalk(int amount = 3)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
            {
                _handler.skipMessages = amount;
                await Context.Message.ReplyAsync($"⚠ Next {amount} message(s) will be ignored");
            }
        }

        [Command("reply chance")]
        [Summary("Change the probability of random replies (%)")]
        [Alias("rc")]
        public async Task ChangeChance(int chance)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
            {
                await Context.Message.ReplyAsync($"⚠ Probability of random replies was changed from {_handler.replyChance}% to {chance}%");
                _handler.replyChance = chance;
            }
        }

        [Command("hunt")]
        [Summary("Reply on every user's message")]
        public async Task Hunt(IUser user)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
            {
                await Context.Message.ReplyAsync($"👻 Hunting {user.Mention}!");
                _handler.huntedUsers.Add(user.Id);
            }
        }

        [Command("unhunt")]
        public async Task Ununt(IUser user)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
            {
                await Context.Message.ReplyAsync($"{user.Mention} is not hunted anymore 👻");
                _handler.huntedUsers.Remove(user.Id);
            }
        }

        [Command("hunt chance")]
        [Summary("Change the probability of replies on a hunted user (%)")]
        [Alias("hc")]
        public async Task HuntChance(int chance)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
            {
                await Context.Message.ReplyAsync($"⚠ Probability of replies for hunted users was changed from {_handler.huntChance}% to {chance}%");
                _handler.huntChance = chance;
            }
        }

        [Command("ignore")]
        [Summary("Prevent user from calling the bot.")]
        public async Task Ignore(IUser user)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
            {
                await Context.Message.ReplyAsync($"⚠ {user.Mention} was added to the blacklist!");
                _handler.blackList.Add(user.Id);
            }
        }

        [Command("allow")]
        [Summary("Allow user to call the bot.")]
        public async Task Allow(IUser user)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
            {
                await Context.Message.ReplyAsync($"⚠ {user.Mention} was removed from the blacklist!");
                _handler.blackList.Remove(user.Id);
            }
        }

        [Command("help")]
        public async Task ShowHelp()
            => await Context.Message.ReplyAsync(
                "`set character<id>` - set character by id\n" +
                "   Aliases: `set`, `sc`\n" +
                "`reset character` - save and start new chat\n" +
                "   Alias: `reset`\n" +
                "`audience toggle` - enable/disable audience mode\n" +
                "   Alias: `amode`\n" +
                "`call user <@user_mention> <any text>` - Make character call other user*(use it to make two bots talk to each other)*\n" +
                "   Aliases: `call`, `cu`\n" +
                "   Example: `@some_character call @another_character Do you love donuts?`\n" +
                "   *(if no text argument provided, default `\"Hey!\"` will be used)*\n" +
                "`skip<amount>` - Make character ignore next few messages *(use it to stop bots' conversation)*\n" +
                "   Alias: `delay`\n" +
                "   *(if no amount argument provided, default `3` will be used)*\n" +
                "   *(commands will not be ignored, amount can be reduced with another call)*\n" +
                "`reply chance <chance>` - Change the probability of random replies on new users' messages `in %` *(It's better to use it with audience mode enabled)*\n" +
                "   Alias: `rc`\n" +
                "   Example: `rc 50` => `Probability of random answers was changed from 0% to 50%`\n" +
                "   *(argument always required)*\n" +
                "   *(keep in mind that with this feature enabled, commands can be executed without bot prefix/mention)*\n" +
                "`hunt <@user_mention>` - Make character always reply on messages of certain user\n" +
                "`unhunt <@user_mention>` - Stop hunting user\n" +
                "`hunt chance <chance>` - Change the probability of replies to hunted user `in %`\n" +
                "   Alias: `hc`\n" +
                "   *(default value = 100%)*\n" +
                "`ignore<@user_mention>` - Prevent user from calling the bot\n" +
                "`allow<@user_mention>` - Allow user to call the bot\n" +
                "`ping` - check latency");

        [Command("ping")]
        public async Task Ping()
            => await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms");
    }
}
