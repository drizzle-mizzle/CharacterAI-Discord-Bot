using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;

namespace CharacterAI_Discord_Bot.Handlers.Commands
{
    public class GlobalCommands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandsHandler _handler;

        public GlobalCommands(CommandsHandler handler)
            => _handler = handler;

        [Command("find character")]
        [Alias("find")]
        public async Task FindCharacter([Remainder] string query)
        {
            if (!ValidatePublic(Context) || !ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                using (Context.Channel.EnterTypingState())
                    _ = FindCharacterAsync(query, _handler, Context);
        }

        [Command("set character")]
        [Alias("sc", "set")]
        public async Task SetCharacter(string charID)
        {
            if (!ValidatePublic(Context) || !ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                using (Context.Channel.EnterTypingState())
                    _ = _handler.SetCharacterAsync(charID, _handler, Context);
        }

        [Command("ignore")]
        [Alias("ban")]
        [Summary("Prevent user from calling the bot.")]
        public async Task Ignore(SocketGuildUser user)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.BlackList.Add(user.Id);
                SaveData(blackList: _handler.BlackList);
                await Context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} {user.Mention} was added to the blacklist!").ConfigureAwait(false);
            }
        }

        [Command("allow")]
        [Alias("unban")]
        [Summary("Allow user to call the bot.")]
        public async Task Allow(SocketGuildUser user)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.BlackList.Remove(user.Id);
                SaveData(blackList: _handler.BlackList);
                await Context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} {user.Mention} was removed from the blacklist!");
            }
        }

        [Command("activity")]
        public async Task UpdateStatus(string status, int type = 0)
        {
            if (!ValidatePublic(Context) || !ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await _handler.SetPlayingStatusAsync(Context.Client, status: status, type: type);
        }

        [Command("status")]
        public async Task UpdateStatus(int status)
        {
            if (!ValidatePublic(Context) || !ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await Context.Client.SetStatusAsync((UserStatus)status);
        }

        [Command("reboot")]
        public async Task RebootBrowser()
        {
            if (!ValidatePublic(Context) || !ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                _ = _handler.CurrentIntegration.LaunchChromeAsync(BotConfig.CustomChromePath, BotConfig.CustomChromeExecPath);
        }
    }
}

