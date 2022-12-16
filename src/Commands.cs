using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using Discord.Commands;

namespace CharacterAI_Discord_Bot
{
    public class Commands
    {
        private readonly DiscordSocketClient _client;
        private Integration _integration;

        public Commands(DiscordSocketClient client, Integration integration)
        {
            _client = client;
            _integration = integration;
        }

        public async Task SetCharacter(string charID, SocketCommandContext context)
        {
            if (!_integration.Setup(charID)) { await context.Message.ReplyAsync("⚠️ Failed to set character!"); return; }

            // Setting bot name
            await context.Guild.GetUser(_client.CurrentUser.Id).ModifyAsync(u => { u.Nickname = _integration._charInfo.Name; });
            // Setting bot playing status with a link to the character
            await context.Client.SetGameAsync($"https://beta.character.ai/chat?char={_integration._charInfo.CharID}");
            // Setting bot avatar
            try
            {
                var image = new Image(new FileStream(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "characterAvatar.avif", FileMode.Open));
                await context.Client.CurrentUser.ModifyAsync(u => { u.Avatar = image; });
            }
            catch { Integration.Log("⚠️ Failed to set bot avatar!", ConsoleColor.Red); }

            await context.Message.ReplyAsync(_integration._charInfo.Greeting);

            return;
        }

        public async Task AudienceToggle(SocketCommandContext context)
        {
            if (_integration == null) return;

            _integration._audienceMode = !_integration._audienceMode;
            string msg = _integration._audienceMode ? "⚠ Audience mode enabled!" : "⚠ Audience mode disabled!";

            await context.Message.ReplyAsync(msg);
        }

        public async Task Ping(SocketCommandContext context)
        {
            await context.Message.ReplyAsync($"Pong! - {context.Client.Latency} ms");
        }
    }
}
