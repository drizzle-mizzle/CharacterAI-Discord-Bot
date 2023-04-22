using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;


namespace CharacterAI_Discord_Bot.Handlers.Commands
{
    /// <summary>
    /// Commands with simpliest logic that doesn't change any data.
    /// </summary>
    public class OneShotCommands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandsHandler _handler;
        public OneShotCommands(CommandsHandler handler)
            => _handler = handler;

        [Command("call user")]
        [Summary("Make character call other user")]
        [Alias("call", "cu")]
        public async Task CallUser(SocketGuildUser user, [Remainder] string msg = "Hey!")
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await Context.Channel.SendMessageAsync(user.Mention + " " + msg).ConfigureAwait(false);
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
                "`ping` – Check latency",
                "`add <channel_id> <user>` – Add user to your private chat",
                "`kick <@user>` – Kick user from your private chat *(use it **in** channel you want to kick user from)*",
                "`private` – Create private chat with a character *(every time user without bot role (or not server owner) creates new chat, his previous chat becomes inactive)*\n    Alias: `pc`",
                "`get history` - Get history id for a current channel\n    Alias: `gh`",
            };
            var managerCommands = new string[]
            {
                "`set history <id>` - Set history id for a current channel\n    Alias: `sh`",
                "`continue history <#channel>` - Copy history id from another channel\n    Aliases: `continue`, `ch`",
                "`find character <query>` – Find and set character by it's name\n    Alias: `find`",
                "`set character <id>` – Set character by id\n    Aliases: `set`, `sc`",
                "`reset character` – Start new chat with a character *(for current text channel only, other channels won't be affected)*\n    Alias: `reset`",
                "`clear` – Delete all inactive private channels",
                "`audience mode <mode?>` – Enable/disable audience mode *(What is the audience mode - read below)*\n    Alias: `amode`\n    Mode: `0` – disabled, `1` – username only, `2` – quote only, `3` – quote and username",
                "`call user <@user> <any text>` – Make character call other user *(you can use it to make two bots talk to each other)*\n    Aliases: `call`, `cu`\n    Example: `@some_character call @another_character Do you love donuts?`\n    *(if no text argument provided, default `\"Hey!\"` will be used)*",
                "`skip <amount>` – Make character ignore next few messages *(use it to stop bots' conversation)*\n    *(if no amount argument provided, default `1` will be used)*\n    *(commands will not be ignored, amount can be reduced with another call)*",
                "`delay <time in seconds>` - Add delay to character responeses *(may be useful if you don't want two bots to reply to each other too fast)*",
                "`reply chance <chance>` – Change the probability of random replies on new users' messages `in %` *(It's better to use it with audience mode enabled)*\n    Alias: `rc`\n    Example: `rc 50` => `Probability of random answers was changed from 0% to 50%`\n    *(default value = 0%)*\n    *(keep in mind that with this feature enabled, commands can be executed without bot prefix/mention)*",
                "`hunt <@user>` – Make character always reply on messages of a certain user",
                "`unhunt <@user>` – Stop hunting user",
                "`hunt chance <@user> <chance>` – Change the probability of replies to a hunted user `in %`\n    Alias: `hc`\n    Example: `hc @user 50` => `Probability of replies for @user was changed from 100% to 50%`\n    *(default value = 100%)*",
                "`ignore <@user>` – Prevent user from calling the bot\n    Alias: `ban`",
                "`allow <@user>` – Allow user to call the bot\n    Alias: `unban`",
                "`activity <text> <type>` – Change bot activity status\n    Type: `0` – Playing, `1` – Streaming, `2` – Listening, `3` – Watching, `4` – Custom (not working), `5` – Competing\n    *(default `type` value = 0)*\n    *(provide `0` for `text` to clear activity)*",
                "`status <type>` – Change bot presence status\n    Type: `0` – Offline, `1` – Online, `2` – Idle, `3` – AFK, `4` – DND, `5` – Invisible",
                "`reboot` - Relaunch browser. Use it if character begings to respond way too slow"
            };
            var publicCommands = new string[]
            {
                "`set history <id>` - Set history id for a current channel\n    Alias: `sh`",
                "`continue history <#channel>` - Copy history id from another channel\n    Alias: `ch`",
                "`reset character` – Start new chat with a character *(for current text channel only, other channels won't be affected)*\n    Alias: `reset`",
                "`clear` – Delete all inactive private channels",
                "`audience mode <mode?>` – Enable/disable audience mode *(What is the audience mode - read below)*\n    Alias: `amode`\n    Mode: `0` – disabled, `1` – username only, `2` – quote only, `3` – quote and username",
                "`call user <@user> <any text>` – Make character call other user *(you can use it to make two bots talk to each other)*\n    Aliases: `call`, `cu`\n    Example: `@some_character call @another_character Do you love donuts?`\n    *(if no text argument provided, default `\"Hey!\"` will be used)*",
                "`skip <amount>` – Make character ignore next few messages *(use it to stop bots' conversation)*\n    *(if no amount argument provided, default `1` will be used)*\n    *(commands will not be ignored, amount can be reduced with another call)*",
                "`delay <time in seconds>` - Add delay to character responeses *(may be useful if you don't want two bots to reply to each other too fast)*",
                "`reply chance <chance>` – Change the probability of random replies on new users' messages `in %` *(It's better to use it with audience mode enabled)*\n    Alias: `rc`\n    Example: `rc 50` => `Probability of random answers was changed from 0% to 50%`\n    *(default value = 0%)*\n    *(keep in mind that with this feature enabled, commands can be executed without bot prefix/mention)*",
                "`hunt <@user>` – Make character always reply on messages of a certain user",
                "`unhunt <@user>` – Stop hunting user",
                "`hunt chance <@user> <chance>` – Change the probability of replies to a hunted user `in %`\n    Alias: `hc`\n    Example: `hc @user 50` => `Probability of replies for @user was changed from 100% to 50%`\n    *(default value = 100%)*",
                "`ignore <@user>` – Prevent user from calling the bot\n    Alias: `ban`",
                "`allow <@user>` – Allow user to call the bot\n    Alias: `unban`",
            };


            if (Context.Guild is null) // DM commands
                await Context.Message.ReplyAsync($"{userCommands[0]}\n{userCommands[1]}\n{userCommands[5]}\n{managerCommands[0]}\n{managerCommands[4]}");
            else if (!ValidateUserAccess(Context))
                await Context.Message.ReplyAsync(string.Join("\n", userCommands));
            else
            {
                var commands = BotConfig.PublicMode ? publicCommands : managerCommands;
                float commandsCount = commands.Length + userCommands.Length;
                var pages = Math.Ceiling((double)(commandsCount / 5.0));

                string text;

                if (page == 1)
                    text = $"**Page 1/{pages}:**\n" + string.Join("\n", userCommands);
                else // 2: 0..4, 3: 5..9, 4: 10..14
                {
                    int posA = page * 5 - 10;
                    int posOverflow = posA + 4 - commands.Length;
                    int posB = posOverflow > 0 ? posA + posOverflow : posA + 5;

                    text = $"**Page {page}/{pages}:**\n" + string.Join("\n", commands[posA..posB]);
                }
                await Context.Message.ReplyAsync(text);
            }
        }

        [Command("servers-list")]
        public async Task Servers()
        {
            if (Context.Message.Author.Id != BotConfig.HosterDiscordId) return;

            var guilds = Context.Client.Guilds;
            string servers = "```\n";
            int count = 0;

            foreach (var guild in guilds)
            {
                servers += $"[{guild.Name}]\n" +
                           $"{(guild.Description is string desc ? $"Description: \"{desc}\"\n" : "")}" +
                           $"Owner: {guild.Owner.DisplayName}{(guild.Owner.Nickname is string nick ? $" / {nick}" : "")}\n" +
                           $"Members: {guild.MemberCount}\n\n";
                count++;
            }
            servers = $"Servers: {count}\n\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\n" + servers + "\n```";
            await Context.Channel.SendMessageAsync(servers);
        }

        [Command("ping")]
        public async Task Ping()
            => await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms").ConfigureAwait(false);
    }
}
