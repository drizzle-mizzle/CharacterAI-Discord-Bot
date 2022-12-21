using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static CharacterAI_Discord_Bot.Service.CommonService;

namespace CharacterAI_Discord_Bot.Service
{

    public class NewCommands : ModuleBase<SocketCommandContext>
    {
        private readonly MessageHandler _handler;

        public NewCommands(MessageHandler handler)
        {
            _handler = handler;
        }

        [Command("set character")]
        [Alias("sc", "set")]
        public async Task SetCharacter(string charID)
        {
            if (!ValidateBotRole()) { await NoPermissionAlert(); return; }
            if (!await _handler.integration.Setup(charID)) { await Context.Message.ReplyAsync("⚠️ Failed to set character!"); return; }

            var charInfo = _handler.integration.charInfo;
            string reply = charInfo.Greeting + "\n" + charInfo.Description;

            try
            {   // Setting bot username
                var botAsGuildUser = Context.Guild.GetUser(Context.Client.CurrentUser.Id);
                await botAsGuildUser.ModifyAsync(u => { u.Nickname = charInfo.Name; });
            }
            catch { reply += "\n⚠️ Failed to set bot name! Probably, missing permissions?"; }

            try
            {   // Setting bot avatar
                var bot = Context.Client.CurrentUser;
                using var fs = new FileStream(avatarPath, FileMode.Open);
                await bot.ModifyAsync(u => { u.Avatar = new Discord.Image(fs); });
            }
            catch { reply += "\n⚠️ Failed to set bot avatar!"; }

            // Setting bot playing status
            await UpdatePlayingStatus();
            // Report status
            await Context.Message.ReplyAsync(reply);
        }

        [Command("audience toggle")]
        [Summary("Enable/disable audience mode ")]
        [Alias("au mode", "amode")]
        public async Task AudienceToggle()
        {
            if (!ValidateBotRole()) { await NoPermissionAlert(); return; }

            var aM = _handler.integration.audienceMode ^= true;

            await UpdatePlayingStatus();
            await Context.Message.ReplyAsync("⚠ Audience mode " + (aM ? "enabled" : "disabled") + '!');
        }

        [Command("ping")]
        public async Task Ping()
        {
            await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms");
        }

        private bool ValidateBotRole()
        {
            var user = Context.User as SocketGuildUser;
            if (user.Id == Context.Guild.OwnerId) return true;

            var roles = (user as IGuildUser).Guild.Roles;
            var requiredRole = roles.FirstOrDefault(role => role.Name == Config.botRole);

            return user.Roles.Contains(requiredRole);
        }

        private async Task NoPermissionAlert()
        {
            if (string.IsNullOrEmpty(nopowerPath) || !File.Exists(nopowerPath)) return;

            var mRef = new MessageReference(messageId: Context.Message.Id);
            await Context.Channel.SendFileAsync(nopowerPath, messageReference: mRef);
        }

        private async Task UpdatePlayingStatus()
        {
            string? charID = _handler.integration.charInfo.CharID;
            string desc = charID == null ? "No character selected | " : $"Description: {_handler.integration.charInfo.Title} | ";

            await Context.Client.SetGameAsync(desc + $"Audience mode: " + (_handler.integration.audienceMode ? "✔️" : "✖️"));
        }
    }
}
