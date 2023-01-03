using Discord;
using Discord.Commands;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;

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
            if (ValidateBotRole(Context)) 
                return Task.Run(() =>
                    SetCharacterAsync(charID, _handler, Context).ConfigureAwait(false)
                );
            else 
                return NoPermissionAlert(Context);
        }

        [Command("audience toggle")]
        [Summary("Enable/disable audience mode ")]
        [Alias("amode")]
        public Task AudienceToggle()
        {
            if (!ValidateBotRole(Context))
                return NoPermissionAlert(Context);

            var amode = _handler.integration.audienceMode ^= true;
            return Task.Run(() =>
            {
                UpdatePlayingStatus(_handler.integration.charInfo, Context.Client, amode).ConfigureAwait(false);
                Context.Message.ReplyAsync("⚠ Audience mode " + (amode ? "enabled" : "disabled") + '!');
            });
        }

        [Command("ping")]
        public async Task Ping()
        {
            await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms");
        }

    }
}
