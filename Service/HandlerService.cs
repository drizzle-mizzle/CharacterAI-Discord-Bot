using Discord.WebSocket;
using Discord;
using System.Text.RegularExpressions;
namespace CharacterAI_Discord_Bot.Service
{
    public partial class HandlerService : CommonService
    {
        public static async Task<ulong> ReplyOnMessage(SocketUserMessage message, dynamic reply)
        {
            string replyText = reply.text;
            string replyImage = reply.image_rel_path ?? "";
            // (3 or more) "\n\n\n..." -> (exactly 2) "\n\n"
            replyText = LBRegex().Replace(replyText, "\n\n");

            IUserMessage? botReply;
            // If has attachments
            if (replyImage != "" && await DownloadImg(replyImage) is byte[] image)
                botReply = await ReplyWithImage(message, image, replyText);
            else // If no attachments
                botReply = await message.ReplyAsync(replyText);

            await SetArrowButtons(botReply);

            return botReply!.Id;
        }

        public static async Task<IUserMessage?> ReplyWithImage(SocketUserMessage message, byte[] image, string text)
        {
            using Stream mc = new MemoryStream(image);
            try
            {
                if (File.Exists(tempImgPath)) File.Delete(tempImgPath);
                using var file = File.Create(tempImgPath);
                mc.CopyTo(file);
            }
            catch (Exception e) { FailureLog("Something went wrong...\n" + e.ToString()); return null; }

            var mRef = new MessageReference(messageId: message.Id);

            return await message.Channel.SendFileAsync(tempImgPath, text, messageReference: mRef);
        }

        public static async Task RemoveButtons(ulong lastMsgId, SocketUserMessage message)
        {
            if (lastMsgId != 0)
            {
                var lastMessage = await message.Channel.GetMessageAsync(lastMsgId);
                await lastMessage.RemoveAllReactionsAsync().ConfigureAwait(false);
            }
        }

        [GeneratedRegex("(\\n){3,}")]
        private static partial Regex LBRegex();
    }
}
