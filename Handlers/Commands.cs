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

        [Command("set character")]
        [Alias("sc", "set")]
        public Task SetCharacter(string charID)
        {
            if (!ValidateBotRole(Context)) NoPermissionAlert(Context);
            else Task.Run(() => SetCharacterAsync(charID, _handler.integration, Context));

            return Task.CompletedTask;
        }

        [Command("audience toggle")]
        [Summary("Enable/disable audience mode ")]
        [Alias("amode")]
        public async Task AudienceToggle()
        {
            if (!ValidateBotRole(Context)) { NoPermissionAlert(Context); return; }

            var amode = _handler.integration.audienceMode ^= true;

            await UpdatePlayingStatus(_handler.integration.charInfo, Context.Client, amode); ;
            await Context.Message.ReplyAsync("⚠ Audience mode " + (amode ? "enabled" : "disabled") + '!');
        }

        [Command("ping")]
        public async Task Ping()
        {
            await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms");
        }

    }
}
