using Discord;
using Discord.Commands;
using System.Threading.Channels;
using System.Xml.Linq;
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
        [Alias("sc", "set!")]
        public async Task SetCharacter(string charID)
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
                await Task.Run(() => SetCharacterAsync(charID, _handler, Context).ConfigureAwait(false));
        }

        [Command("reset character")]
        [Alias("reset!")]
        public async Task ResetCharacter()
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
                await Task.Run(() => ResetCharacterAsync(_handler, Context).ConfigureAwait(false));
        }

        [Command("audience toggle")]
        [Summary("Enable/disable audience mode")]
        [Alias("amode!")]
        public async Task AudienceToggle()
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else 
            {
                var amode = _handler.integration.audienceMode ^= true;
                await UpdatePlayingStatus(_handler.integration.charInfo, Context.Client, amode).ConfigureAwait(false);
                await Context.Message.ReplyAsync("⚠ Audience mode " + (amode ? "enabled" : "disabled") + '!');
            }
        }

        [Command("call user")]
        [Summary("Make character call other user")]
        [Alias("call!", "cu")]
        public async Task CallUser(IUser user, string msg = "Hey!")
        {
            if (!ValidateBotRole(Context)) await NoPermissionAlert(Context);
            else
                await Context.Channel.SendMessageAsync(user.Mention + " " + msg);
        }

        [Command("skip!")]
        [Summary("Make character ignore next few messages")]
        [Alias("delay!")]
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

        [Command("hunt!")]
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

        [Command("unhunt!")]
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
        [Summary("Change the probability of replies on hunted user (%)")]
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

        [Command("ping!")]
        public async Task Ping()
            => await Context.Message.ReplyAsync($"Pong! - {Context.Client.Latency} ms");
    }
}
