using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static CharacterAI_Discord_Bot.Service.CommonService;

namespace CharacterAI_Discord_Bot.Service
{
    public class HandlerService : CommonService
    {
        public static async Task<string> ReplyOnMessage(SocketUserMessage message, dynamic reply)
        {
            string replyText = reply.text;
            string replyImage = reply.image_rel_path ?? "";
            // (3 or more) "\n\n\n..." -> (exactly 2) "\n\n"
            replyText = new Regex("(\\n){3,}").Replace(replyText, "\n\n");

            IUserMessage? botReply;
            // If has attachments
            if (replyImage != "" && await DownloadImg(replyImage) is byte[] image)
                botReply = await ReplyWithImage(message, image, replyText);
            else // If no attachments
                botReply = await message.ReplyAsync(replyText);

            await SetArrowButtons(botReply);

            return botReply!.Id.ToString();
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
    }
}
