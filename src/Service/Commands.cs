using Discord.Commands;
using Discord.WebSocket;
using Discord;

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
            if (!ValidateBotRole()) { await NoPermission(); return; }
            if (!_handler.integration.Setup(charID)) { await Context.Message.ReplyAsync("⚠️ Failed to set character!"); return; }

            var charInfo = _handler.integration.charInfo;
            string reply = charInfo.Greeting + "\n" + charInfo.Description;

            // Setting bot username
            try { await Context.Guild.GetUser(Context.Client.CurrentUser.Id).ModifyAsync(u => { u.Nickname = charInfo.Name; }); }
            catch { reply += "\n⚠️ Failed to set bot name! Probably, missing permissions?"; }

            // Setting bot avatar
            try { await Context.Client.CurrentUser.ModifyAsync(u => { u.Avatar = new Image(new FileStream(_handler.pfpPath, FileMode.Open)); }); }
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
            if (!ValidateBotRole()) { await NoPermission(); return; }

            var aM = _handler.integration.audienceMode ^= true;

            await UpdatePlayingStatus();
            await Context.Message.ReplyAsync("⚠ Audience mode " + (aM ? "enabled" : "disabled") + '!');
        }

        [Command("ping")]
        public async Task Ping()
        {
            if (!ValidateBotRole()) { await NoPermission(); return; }

            await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms");
        }

        private bool ValidateBotRole()
        {
            var user = Context.User as SocketGuildUser;
            var role = (user as IGuildUser).Guild.Roles.FirstOrDefault(role => role.Name == CommonService.GetConfig().botRole);
            return user.Roles.Contains(role);
        }

        private async Task NoPermission()
        {
            var mRef = new MessageReference(messageId: Context.Message.Id);
            await Context.Channel.SendFileAsync(_handler.nopowerPath, messageReference: mRef);
        }

        private async Task UpdatePlayingStatus()
        {
            string? charID = _handler.integration.charInfo.CharID;
            string desc = charID == null ? "No character selected | " : $"Description: {_handler.integration.charInfo.Title} | ";

            await Context.Client.SetGameAsync(desc + $"Audience mode: " + (_handler.integration.audienceMode ? "✔️" : "✖️"));
        }
    }
}
