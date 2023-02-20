using Discord.WebSocket;
using Discord;
using System.Text.RegularExpressions;
using Discord.Commands;
using System.Dynamic;
using CharacterAI_Discord_Bot.Models;
using CharacterAI.Models;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class HandlerService : CommonService
    {
        public static async Task<ulong> ReplyOnMessage(SocketUserMessage message, Reply reply)
        {
            string replyText = reply.Text!;

            // (3 or more) "\n\n\n..." -> (exactly 2) "\n\n"
            replyText = LBRegex().Replace(replyText, "\n\n");

            Embed? embed = null;
            if (reply.HasImage && await TryGetImage(reply.ImageRelPath!))
                embed = new EmbedBuilder().WithImageUrl(reply.ImageRelPath).Build();

            var botReply = await message.ReplyAsync(replyText, embed: embed).ConfigureAwait(false);

            // Skip adding buttons if delay is 0
            if (BotConfig.RemoveDelay > 0)
                await SetArrowButtons(botReply).ConfigureAwait(false);
                _ = RemoveButtons(botReply, delay: BotConfig.RemoveDelay);

            return botReply!.Id;
        }


        //public static async Task<IUserMessage?> ReplyWithImage(SocketUserMessage message, byte[] image, string text)
        //{
        //    using Stream mc = new MemoryStream(image);
        //    try
        //    {
        //        if (File.Exists(tempImgPath)) File.Delete(tempImgPath);
        //        using var file = File.Create(tempImgPath);
        //        mc.CopyTo(file);
        //    }
        //    catch (Exception e) { Failure("Something went wrong...\n" + e.ToString()); return null; }

        //    var mRef = new MessageReference(messageId: message.Id);

        //    return await message.Channel.SendFileAsync(tempImgPath, text, messageReference: mRef);
        //}

        public static async Task SetArrowButtons(IUserMessage? message)
        {
            if (message is null) return;

            var btn1 = new Emoji("\u2B05");
            var btn2 = new Emoji("\u27A1");

            await message.AddReactionsAsync(new Emoji[] { btn1, btn2 }).ConfigureAwait(false);
        }

        public static async Task RemoveButtons(IMessage? lastMessage = null, int delay = 0)
        {
            if (lastMessage is null) return;

            if (delay > 0)
                await Task.Delay(delay * 1000);

            try { await lastMessage.RemoveAllReactionsAsync().ConfigureAwait(false); } catch { }
        }

        [GeneratedRegex("(\\n){3,}")]
        private static partial Regex LBRegex();
    }
}
