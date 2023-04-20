using Discord.WebSocket;
using Discord;
using System.Text.RegularExpressions;
using CharacterAI.Models;
using CharacterAI_Discord_Bot.Models;
using Discord.Commands;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class HandlerService : CommonService
    {
        internal static Emoji ARROW_LEFT = new("\u2B05");
        internal static Emoji ARROW_RIGHT = new("\u27A1");
        //internal static Emoji REPEAT_BTN = new("\uD83D\uDD04");
        internal static Emoji STOP_BTN = new("\u26D4");
        private readonly List<ulong> _removeEmojiRequestQueue = new ();

        public async Task<ulong> RespondOnMessage(SocketUserMessage message, Reply reply, bool isPrivate)
        {
            string replyText = reply.Text!;

            // (3 or more) "\n\n\n..." -> (exactly 1) "\n"
            replyText = LBRegex().Replace(replyText, "\n");

            if (replyText.Length > 2000)
                replyText = replyText[0..1996] + "...";

            Embed? embed = null;
            if (reply.HasImage && await TryGetImage(reply.ImageRelPath!))
                embed = new EmbedBuilder().WithImageUrl(reply.ImageRelPath).Build();

            var mentions = isPrivate ? AllowedMentions.None : AllowedMentions.All;
            var botReply = await message.ReplyAsync(replyText, embed: embed, allowedMentions: mentions).ConfigureAwait(false);

            bool isReplyToBot = botReply.ReferencedMessage is IUserMessage um && um.Author.IsBot;
            if (!isReplyToBot && BotConfig.SwipesEnabled)
            {
                await AddArrowButtonsAsync(botReply).ConfigureAwait(false);
                if (BotConfig.RemoveDelay != 0)
                    _ = RemoveButtonsAsync(botReply, botReply.Author, delay: BotConfig.RemoveDelay);
            }
            if (isReplyToBot && BotConfig.StopBtnEnabled)
                await AddStopButtonAsync(botReply);

            return botReply!.Id;
        }

        public static async Task AddArrowButtonsAsync(IUserMessage? message)
        {
            if (message is null) return;

            var btns = new Emoji[] { ARROW_LEFT, ARROW_RIGHT };
            await message.AddReactionsAsync(btns).ConfigureAwait(false);
        }

        public static async Task AddStopButtonAsync(IUserMessage? message)
        {
            if (message is null) return;

            await message.AddReactionAsync(STOP_BTN).ConfigureAwait(false);
        }

        //public static async Task AddRepeatButtonAsync(IUserMessage? message)
        //{
        //    if (message is null) return;

        //    await message.AddReactionAsync(REPEAT_BTN).ConfigureAwait(false);
        //}

        public async Task RemoveButtonsAsync(IMessage lastMessage, IUser user, int delay = 0)
        {
            if (delay > 0) await Task.Delay(delay * 1000);
            _removeEmojiRequestQueue.Add(lastMessage.Id);

            while (true)
            {
                if (_removeEmojiRequestQueue.First() != lastMessage.Id)
                {
                    await Task.Delay(1000);
                }
                else
                {
                    try
                    {
                        foreach (var btn in new Emoji[] { ARROW_LEFT, ARROW_RIGHT, STOP_BTN })
                            await lastMessage.RemoveReactionAsync(btn, user).ConfigureAwait(false);
                    }
                    catch { }

                    _removeEmojiRequestQueue.Remove(lastMessage.Id);
                    return;
                }
            }
        }

        /// <summary>
        /// Remove prefix and/or @mention_prefix
        /// </summary>
        public static string RemoveMention(string text)
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

        public static string AddUsername(string text, SocketCommandContext context)
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

        public static string AddQuote(string text, SocketUserMessage message)
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
