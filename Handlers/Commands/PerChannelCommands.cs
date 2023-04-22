using CharacterAI_Discord_Bot.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;

namespace CharacterAI_Discord_Bot.Handlers.Commands
{
    public class PerChannelCommands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandsHandler _handler;
        private DiscordChannel? CurrentChannel => _handler.Channels.Find(c => c.Id == Context.Channel.Id);
        public PerChannelCommands(CommandsHandler handler)
            => _handler = handler;

        [Command("get history")]
        [Alias("gh")]
        public async Task GetHistory()
        {
            if (CurrentChannel is not DiscordChannel cc) return;

            string text = $"Current **history_id**: `{cc.Data.HistoryId}`";
            await Context.Message.ReplyAsync(text).ConfigureAwait(false);
        }

        [Command("set history")]
        [Alias("sh")]
        public async Task SetHistory(string historyId)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is not DiscordChannel cc) return;

                string text = $"{WARN_SIGN_DISCORD} **history_id** for this channel was changed from `{cc.Data.HistoryId}` to `{historyId}`";
                if (historyId.Length != 43)
                    text += $"\nEntered history_id has a length that is different from expected ({historyId.Length}/43). Make sure it's correct.";

                cc.Data.HistoryId = historyId;
                SaveData(channels: _handler.Channels);

                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
        }

        [Command("continue history")]
        [Alias("ch", "continue")]
        public async Task ContinueHistory(SocketGuildChannel channel)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is not DiscordChannel cc) return;
                if (_handler.Channels.Find(c => c.Id == channel.Id) is not DiscordChannel copyChannel) return;

                string? newHistoryId = copyChannel.Data.HistoryId;
                if (newHistoryId is null) return;

                string text = $"{WARN_SIGN_DISCORD} **history_id** for this channel was changed from `{cc.Data.HistoryId}` to `{newHistoryId}`";

                cc.Data.HistoryId = newHistoryId;
                SaveData(channels: _handler.Channels);

                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
        }

        [Command("reset character")]
        [Summary("Drop chat history in current channel")]
        [Alias("reset")]
        public async Task ResetCharacter()
        {
            if (Context.Guild is not null && !ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else if (_handler.CurrentIntegration.CurrentCharacter.IsEmpty)
                await Context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} Set a character first").ConfigureAwait(false);
            else
                using (Context.Channel.EnterTypingState())
                    _ = ResetCharacterAsync(_handler, Context);
        }

        [Command("reply chance")]
        [Summary("Change the probability of random replies (%) in current channel")]
        [Alias("rc")]
        public async Task ChangeChance(float chance)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is not DiscordChannel cc) return;

                string text = $"{WARN_SIGN_DISCORD} Probability of random replies in current channel was changed from {cc.Data.ReplyChance}% to {chance}%";
                await Context.Message.ReplyAsync(text).ConfigureAwait(false);

                cc.Data.ReplyChance = chance;
                SaveData(channels: _handler.Channels);
            }
        }

        [Command("reply delay")]
        [Summary("Wait some time before responding")]
        [Alias("rd", "delay")]
        public async Task ReplyDelay(int delay)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is not DiscordChannel cc) return;

                string text = $"{WARN_SIGN_DISCORD} Replies delay for current channel was changed from {cc.Data.ReplyDelay}s to {delay}s";
                await Context.Message.ReplyAsync(text).ConfigureAwait(false);

                cc.Data.ReplyDelay = delay;
                SaveData(channels: _handler.Channels);
            }
        }

        [Command("audience mode")]
        [Summary("Enable/disable audience mode")]
        [Alias("amode")]
        public async Task AudienceToggle(int? newMode = null)
        {
            if (CurrentChannel is not DiscordChannel cc || Context.Guild is null) return;

            int currentMode = newMode ?? cc.Data.AudienceMode;
            string modeMsg = currentMode switch
            {
                3 => "quote and username",
                2 => "quote only",
                1 => "username only",
                _ => "disabled",
            };

            string text;
            if (newMode is null)
            {
                text = $"{WARN_SIGN_DISCORD} Current mode: {currentMode} - {modeMsg}";
                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
            else if (ValidateUserAccess(Context))
            {
                text = $"{WARN_SIGN_DISCORD} Audience mode: " + modeMsg + '!';
                await Context.Message.ReplyAsync(text).ConfigureAwait(false);

                cc.Data.AudienceMode = currentMode;
                SaveData(channels: _handler.Channels);
            }
            else
                await NoPermissionAlert(Context).ConfigureAwait(false);
        }

        [Command("skip")]
        [Summary("Make character ignore next few messages")]
        public async Task StopTalk(int amount = 1)
        {
            if (!ValidateUserAccess(Context))
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
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is null) return;

                CurrentChannel.Data.HuntedUsers.Add(user.Id, 100);
                await Context.Message.ReplyAsync($"👻 Hunting {user.Mention}!").ConfigureAwait(false);
            }
        }

        [Command("unhunt")]
        public async Task Ununt(SocketGuildUser user)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is null) return;

                CurrentChannel.Data.HuntedUsers.Remove(user.Id);
                await Context.Message.ReplyAsync($"{user.Mention} is not hunted anymore 👻").ConfigureAwait(false);
            }
        }

        [Command("hunt chance")]
        [Summary("Change the probability of replies to a hunted user (%)")]
        [Alias("hc")]
        public async Task HuntChance(SocketGuildUser user, int chance)
        {
            if (!ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is null) return;

                string text = $"{WARN_SIGN_DISCORD} Probability of replies for {user.Mention} was changed from {CurrentChannel.Data.HuntedUsers[user.Id]}% to {chance}%";
                CurrentChannel.Data.HuntedUsers[user.Id] = chance;

                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
        }
    }
}
