using Discord;
using Discord.WebSocket;
using Discord.Commands;
using CharacterAI_Discord_Bot.Service;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterAI_Discord_Bot
{
    public class Commands : CommonService
    {
        private readonly DiscordSocketClient _client;
        private Integration _integration;

        public Commands(DiscordSocketClient client, Integration integration)
        {
            _client = client;
            _integration = integration;
        }

        public async Task SetCharacter(string? charID, SocketCommandContext context)
        {
            if (!_integration.Setup(charID)) { await context.Message.ReplyAsync("⚠️ Failed to set character!"); return; }

            // Setting bot name
            try { await context.Guild.GetUser(_client.CurrentUser.Id).ModifyAsync(u => { u.Nickname = _integration._charInfo.Name; }); }
            catch { await context.Message.ReplyAsync("⚠️ Failed to set bot name! Probably, missing permissions?"); }

            // Setting bot playing status with a link to the character
            await context.Client.SetGameAsync($"https://beta.character.ai/chat?char={_integration._charInfo.CharID}");

            // Setting bot avatar
            try
            {
                using var image = new Discord.Image(new FileStream(botImgPath, FileMode.Open));
                await context.Client.CurrentUser.ModifyAsync(u => { u.Avatar = image; });
            }
            catch { await context.Message.ReplyAsync("⚠️ Failed to set bot avatar!"); }

            await context.Message.ReplyAsync(_integration._charInfo.Greeting);

            return;
        }

        public async Task AudienceToggle(SocketCommandContext context)
        {
            if (_integration == null) return;

            _integration.audienceMode = !_integration.audienceMode;
            await context.Message.ReplyAsync("⚠ Audience mode " + (_integration.audienceMode ? "enabled" : "disabled"));
        }

        public static async Task Ping(SocketCommandContext context)
        {
            await context.Message.ReplyAsync($"Pong! - {context.Client.Latency} ms");
        }
    }
}
