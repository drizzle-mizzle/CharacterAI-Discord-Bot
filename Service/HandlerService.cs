using Discord.WebSocket;
using Discord;
using System.Text.RegularExpressions;
using CharacterAI.Models;
using CharacterAI_Discord_Bot.Models;
using Discord.Commands;
using CharacterAI;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class HandlerService : CommonService
    {
        internal Integration CurrentIntegration { get; set; } = null!;
        internal List<ulong> BlackList { get; set; } = new();
        internal List<DiscordChannel> Channels { get; set; } = new();
        internal LastSearchQuery? LastSearch { get; set; }
        internal Dictionary<ulong, int> RemoveEmojiRequestQueue { get; set; } = new();

        internal static Emoji ARROW_LEFT = new("\u2B05");
        internal static Emoji ARROW_RIGHT = new("\u27A1");
        //internal static Emoji REPEAT_BTN = new("\uD83D\uDD04");
        internal static Emoji STOP_BTN = new("\u26D4");

        internal async Task<ulong> RespondOnMessage(SocketUserMessage message, Reply reply, bool isPrivate)
        {
            string replyText = reply.Text!;

            // (3 or more) "\n\n\n..." -> (exactly 1) "\n"
            replyText = LBRegex().Replace(replyText, "\n");

            // Discord won't accept message that is longer than 2000 symbols
            if (replyText.Length > 2000)
                replyText = replyText[0..1993] + " [...]";

            // Add image to the message
            Embed? embed = null;
            if (reply.HasImage && await TryGetImageAsync(reply.ImageRelPath!, @HttpClient))
                embed = new EmbedBuilder().WithImageUrl(reply.ImageRelPath).Build();

            // Send message
            var mentions = isPrivate ? AllowedMentions.None : AllowedMentions.All;
            var botReply = await message.ReplyAsync(replyText, embed: embed, allowedMentions: mentions).ConfigureAwait(false);

            bool isReplyToBot = botReply.ReferencedMessage is IUserMessage um && um.Author.IsBot;

            // If reply to user, add swipe arrows
            if (!isReplyToBot && BotConfig.SwipesEnabled)
            {
                await AddArrowButtonsAsync(botReply).ConfigureAwait(false);
                if (BotConfig.RemoveDelay > 0)
                    _ = RemoveButtonsAsync(botReply, delay: BotConfig.RemoveDelay);
            }
            // If reply to bot, add stop button
            if (isReplyToBot && BotConfig.StopBtnEnabled)
                await AddStopButtonAsync(botReply);

            return botReply!.Id;
        }

        internal async Task<DiscordChannel> StartTrackingChannelAsync(SocketCommandContext context)
        {
            var cI = CurrentIntegration;
            var data = new CharacterDialogData(null, null);
            var currentChannel = new DiscordChannel(context.Channel.Id, context.User.Id, data);
            Channels.Add(currentChannel);

            if (!cI.CurrentCharacter.IsEmpty)
            {
                var historyId = await cI.CreateNewChatAsync() ?? cI.Chats[0];

                currentChannel.Data.CharacterId = cI.CurrentCharacter.Id;
                currentChannel.Data.HistoryId = historyId;
            }

            SaveData(channels: Channels);

            return currentChannel;
        }

        internal static async Task AddArrowButtonsAsync(IUserMessage? message)
        {
            if (message is null) return;

            var btns = new Emoji[] { ARROW_LEFT, ARROW_RIGHT };
            await message.AddReactionsAsync(btns).ConfigureAwait(false);
        }

        internal static async Task AddStopButtonAsync(IUserMessage? message)
        {
            if (message is null) return;

            await message.AddReactionAsync(STOP_BTN).ConfigureAwait(false);
        }

        //public static async Task AddRepeatButtonAsync(IUserMessage? message)
        //{
        //    if (message is null) return;

        //    await message.AddReactionAsync(REPEAT_BTN).ConfigureAwait(false);
        //}

        internal async Task RemoveButtonsAsync(IMessage lastMessage, int delay = 0)
        {
            // Add request to the end of the line
            ulong key = lastMessage.Id;
            RemoveEmojiRequestQueue.Add(key, value: delay);
            // Delay it till it will take the first place
            while (RemoveEmojiRequestQueue.First().Key != key)
                await Task.Delay(200);

            // Wait for actual "remove delay" to become 0
            // Delay can be updated outside of this method
            while (RemoveEmojiRequestQueue[key] > 0)
            {
                await Task.Delay(1000);
                RemoveEmojiRequestQueue[key]--;
            }
            // May fail because of permissions or some connection problems
            await TryToRemoveEmojisAsync(lastMessage);
            // Remove from the line
            RemoveEmojiRequestQueue.Remove(key);
        }

        private static async Task TryToRemoveEmojisAsync(IMessage message)
        {
            try { // don't look here, I know it's horrible
                foreach (var btn in new Emoji[] { ARROW_LEFT, ARROW_RIGHT, STOP_BTN })
                    await message.RemoveReactionAsync(btn, message.Author).ConfigureAwait(false); }
            catch { }
        }

        /// <summary>
        /// Remove prefix and/or @mention_prefix
        /// </summary>
        internal static string RemoveMention(string text)
        {
            text = text.Trim();
            // Remove first @mention
            if (text.StartsWith("<"))
                text = new Regex("\\<(.*?)\\>").Replace(text, "", 1);
            // Remove prefix
            var prefixes = BotConfig.BotPrefixes.OrderBy(p => p.Length).Reverse().ToArray(); // ex: "~ai" first, only then "~"
            foreach (string prefix in prefixes)
                if (text.StartsWith(prefix))
                    text = text.Replace(prefix, "");

            return text;
        }

        internal static string AddUsername(string text, SocketCommandContext context)
        {
            string name;
            if (context.Guild is null)
                name = context.User.Username;
            else
            {
                var guildUser = context.User as SocketGuildUser;
                name = guildUser?.Nickname ?? guildUser!.Username;
            }

            string username = BotConfig.AudienceModeNameFormat.Replace("{username}", name);
            if (!string.IsNullOrWhiteSpace(text))
                text = username + text;

            return text;
        }

        internal static string AddQuote(string text, SocketUserMessage message)
        {
            var refMsg = message.ReferencedMessage;
            bool hasRefMessage = refMsg is not null && !string.IsNullOrEmpty(refMsg.Content);
            if (hasRefMessage)
            {
                string quote = BotConfig.AudienceModeQuoteFormat.Replace("{quote}", refMsg!.Content);
                text = quote + RemoveMention(text);
            }

            return text;
        }


        [GeneratedRegex("(\\n){3,}")]
        private static partial Regex LBRegex();
    }
}
