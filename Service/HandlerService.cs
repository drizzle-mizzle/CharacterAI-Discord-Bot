using Discord.WebSocket;
using Discord;
using System.Text.RegularExpressions;
using CharacterAI.Models;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class HandlerService : CommonService
    {
        internal static Emoji ARROW_LEFT = new("\u2B05");
        internal static Emoji ARROW_RIGHT = new("\u27A1");
        internal static Emoji STOP_BTN = new("\u26D4");

        public static async Task<ulong> ReplyOnMessage(SocketUserMessage message, Reply reply, bool isPrivate)
        {
            string replyText = reply.Text!;

            // (3 or more) "\n\n\n..." -> (exactly 1) "\n"
            replyText = LBRegex().Replace(replyText, "\n");

            Embed? embed = null;
            if (reply.HasImage && await TryGetImage(reply.ImageRelPath!))
                embed = new EmbedBuilder().WithImageUrl(reply.ImageRelPath).Build();

            var mentions = isPrivate ? AllowedMentions.None : AllowedMentions.All;

            await Task.Delay(BotConfig.RepliesDelay * 1000);
            var botReply = await message.ReplyAsync(replyText, embed: embed, allowedMentions: mentions).ConfigureAwait(false);

            bool replyToBot = message.ReferencedMessage is not null && message.ReferencedMessage.Author.IsBot;
            if (!replyToBot && BotConfig.SwipesEnabled)
            {
                await AddArrowButtons(botReply).ConfigureAwait(false);
                if (BotConfig.RemoveDelay != 0)
                    _ = RemoveButtons(botReply, botReply.Author, delay: BotConfig.RemoveDelay);
            }
            if (replyToBot && BotConfig.StopBtnEnabled)
                await AddStopButton(botReply);

            return botReply!.Id;
        }

        public static async Task AddArrowButtons(IUserMessage? message)
        {
            if (message is null) return;

            var btns = new Emoji[] { ARROW_LEFT, ARROW_RIGHT };
            await message.AddReactionsAsync(btns).ConfigureAwait(false);
        }

        public static async Task AddStopButton(IUserMessage? message)
        {
            if (message is null) return;

            await message.AddReactionAsync(STOP_BTN).ConfigureAwait(false);
        }

        public static async Task RemoveButtons(IMessage lastMessage, IUser user, int delay = 0)
        {
            if (delay > 0)
                await Task.Delay(delay * 1000);

            try
            {
                foreach (var btn in new Emoji[] { ARROW_LEFT, ARROW_RIGHT, STOP_BTN })
                    await lastMessage.RemoveReactionAsync(btn, user).ConfigureAwait(false);
            }
            catch { }
        }

        [GeneratedRegex("(\\n){3,}")]
        private static partial Regex LBRegex();
    }
}
