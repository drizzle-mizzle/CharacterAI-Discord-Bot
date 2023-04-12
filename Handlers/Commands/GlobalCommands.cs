using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;
using CharacterAI_Discord_Bot.Models;

namespace CharacterAI_Discord_Bot.Handlers.Commands
{
    public class GlobalCommands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandsHandler _handler;
        private Channel? CurrentChannel => _handler.Channels.Find(c => c.Id == Context.Channel.Id);
        public GlobalCommands(CommandsHandler handler)
            => _handler = handler;

        [Command("find character")]
        [Alias("find")]
        public async Task FindCharacter([Remainder] string query)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                using (Context.Message.Channel.EnterTypingState())
                    _ = FindCharacterAsync(query, _handler, Context);
        }

        [Command("set character")]
        [Alias("sc", "set")]
        public async Task SetCharacter(string charID)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                using (Context.Message.Channel.EnterTypingState())
                    _ = SetCharacterAsync(charID, _handler, Context);
        }

        [Command("skip")]
        [Summary("Make character ignore next few messages")]
        public async Task StopTalk(int amount = 1)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is null) return;

                CurrentChannel.Data.SkipMessages = amount;
                await Context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} Next {amount} message(s) will be ignored").ConfigureAwait(false);
            }
        }

        [Command("hunt")]
        [Summary("Reply on each message of a certain user")]
        public async Task Hunt(SocketGuildUser user)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.HuntedUsers.Add(user.Id, 100);
                await Context.Message.ReplyAsync($"👻 Hunting {user.Mention}!").ConfigureAwait(false);
            }
        }

        [Command("unhunt")]
        public async Task Ununt(SocketGuildUser user)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                _handler.HuntedUsers.Remove(user.Id);
                await Context.Message.ReplyAsync($"{user.Mention} is not hunted anymore 👻").ConfigureAwait(false);
            }
        }

        [Command("hunt chance")]
        [Summary("Change the probability of replies to a hunted user (%)")]
        [Alias("hc")]
        public async Task HuntChance(SocketGuildUser user, int chance)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                string text = $"{WARN_SIGN_DISCORD} Probability of replies for {user.Mention} was changed from {_handler.HuntedUsers[user.Id]}% to {chance}%";
                _handler.HuntedUsers[user.Id] = chance;

                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
        }

        [Command("ignore")]
        [Alias("ban")]
        [Summary("Prevent user from calling the bot.")]
        public async Task Ignore(SocketGuildUser user)
        {
            if (!ValidateBotRole(Context))
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
            if (!ValidateBotRole(Context))
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
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await SetPlayingStatusAsync(Context.Client, status: status, type: type);
        }

        [Command("status")]
        public async Task UpdateStatus(int status)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
                await Context.Client.SetStatusAsync((UserStatus)status);
        }
    }
}

