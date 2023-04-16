using CharacterAI_Discord_Bot.Models;
using Discord;
using Discord.Commands;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;

namespace CharacterAI_Discord_Bot.Handlers.Commands
{
    public class PerChannelCommands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandsHandler _handler;
        private Channel? CurrentChannel => _handler.Channels.Find(c => c.Id == Context.Channel.Id);
        public PerChannelCommands(CommandsHandler handler)
            => _handler = handler;

        [Command("set history")]
        [Alias("sh", "history")]
        public async Task GetSetHistory(string? historyId = null)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is not Channel cc) return;

                string text;
                if (historyId is null)
                    text = $"Current **history_id**: `{cc.Data.HistoryId}`";
                else
                {
                    text = $"{WARN_SIGN_DISCORD} **history_id** for this channel was changed from `{cc.Data.HistoryId}` to `{historyId}`";

                    cc.Data.HistoryId = historyId;
                    SaveData(channels: _handler.Channels);
                }

                await Context.Message.ReplyAsync(text).ConfigureAwait(false);
            }
        }

        [Command("reset character")]
        [Summary("Drop chat history in current channel")]
        [Alias("reset")]
        public async Task ResetCharacter()
        {
            if (Context.Guild is not null && !ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else if (_handler.CurrentIntegration.CurrentCharacter.IsEmpty)
                await Context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} Set a character first").ConfigureAwait(false);
            else
                using (Context.Message.Channel.EnterTypingState())
                    _ = ResetCharacterAsync(_handler, Context);
        }

        [Command("reply chance")]
        [Summary("Change the probability of random replies (%) in current channel")]
        [Alias("rc")]
        public async Task ChangeChance(float chance)
        {
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is not Channel cc) return;

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
            if (!ValidateBotRole(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else
            {
                if (CurrentChannel is not Channel cc) return;

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
            if (CurrentChannel is not Channel cc || Context.Guild is null) return;

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
            else if (ValidateBotRole(Context))
            {
                text = $"{WARN_SIGN_DISCORD} Audience mode: " + modeMsg + '!';
                await Context.Message.ReplyAsync(text).ConfigureAwait(false);

                cc.Data.AudienceMode = currentMode;
                SaveData(channels: _handler.Channels);
            }
            else
                await NoPermissionAlert(Context).ConfigureAwait(false);
        }
    }
}
