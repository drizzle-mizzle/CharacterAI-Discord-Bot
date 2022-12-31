using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterAI_Discord_Bot.Service
{

    public class MessageHandler : CommonService
    {
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;
        private int _lastReplyNumber;
        private dynamic? _lastReplies;
        private string? _currReplyID;
        private string? _lastUserReplyID;
        private string _lastBotMsgID;
        public readonly Integration integration;

        public MessageHandler(IServiceProvider services)
        {
            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            integration = new Integration(Config.userToken);

            _client.MessageReceived += HandleMessage;
            _client.ReactionAdded += HandleReaction;
        }

        private Task HandleMessage(SocketMessage rawMessage)
        {
            var message = rawMessage as SocketUserMessage;
            if (message is null) return Task.CompletedTask;

            int argPos = 0;
            string[] prefixes = Config.botPrefixes;

            bool hasMention = message.HasMentionPrefix(_client.CurrentUser, ref argPos);
            bool hasPrefix = !hasMention && prefixes.Any(p => message.HasStringPrefix(p, ref argPos));
            bool hasReply = !hasPrefix && !hasMention && message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id; // SO FUCKING BIG UUUGHH!

            if (hasMention || hasPrefix || hasReply)
            {
                var context = new SocketCommandContext(_client, message);
                var cmdResponse = _commands.ExecuteAsync(context, argPos, _services).Result;

                if (!cmdResponse.IsSuccess)
                    using (message.Channel.EnterTypingState())
                        Task.Run(() => CallCharacterAsync(message));
            }

            return Task.CompletedTask;
        }

        private Task HandleReaction(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (_lastReplies is null || rawMessage.Id.ToString() != _lastBotMsgID)
                return Task.CompletedTask;

            var message = rawMessage.DownloadAsync().Result;
            var user = reaction.User.Value as SocketGuildUser;
            if (user.IsBot || user.Id != message.ReferencedMessage.Author.Id)
                return Task.CompletedTask;

            bool changed = false;
            // left arrow
            if (reaction.Emote.Name == new Emoji("\u27A1").Name)
            {
                _lastReplyNumber++;
                changed = true;
            } // right arrow
            else if (reaction.Emote.Name == new Emoji("\u2B05").Name && _lastReplyNumber > 0)
            {
                _lastReplyNumber--;
                changed = true;
            }

            if (changed)
                Task.Run(() => UpdateMessageAsync(message));

            return Task.CompletedTask;
        }

        private async Task UpdateMessageAsync(IUserMessage message)
        {
            dynamic newReply;
            try { newReply = _lastReplies![_lastReplyNumber]; }
            catch {
                await message.ModifyAsync(msg => { msg.Content = $"( 🕓 Wait... )"; });
                var response = await integration.CallCharacter("", "", parentMsg: _lastUserReplyID);
                if (response is string) return;

                _lastReplies.Merge(response!.replies);
                newReply = _lastReplies![_lastReplyNumber];
            }

            _currReplyID = newReply.id;
            if (newReply.image_rel_path != null)
            {
                // There's no way to modify attachments in message, so we just delete it and send new one
                var refMsg = message.ReferencedMessage as SocketUserMessage;
                await message.DeleteAsync();

                _lastBotMsgID = await ReplyOnMessage(refMsg, newReply);
            }
            else
            {
                await message.ModifyAsync(msg => { msg.Content = $"{newReply.text}"; });
            }
        }

        private async Task CallCharacterAsync(SocketUserMessage message)
        {
            // Return if try to call a character before it was set up
            if (integration.charInfo.CharID == null) { await message.ReplyAsync("⚠ Set a character first"); return; }

            _lastReplies = null;
            _lastReplyNumber = 0;
            
            string text = RemoveMention(message.Content);
            string imgPath = "";

            // Prepare call data
            if (integration.audienceMode)
                text = MakeItThreadMessage(text, message);
            if (message.Attachments.Any())
            {   // Downloads first image from attachments and uploads it to server
                string url = message.Attachments.First().Url;
                if ((await DownloadImg(url) is byte[] img) && integration.UploadImg(img) is Task<string> path)
                    imgPath = $"https://characterai.io/i/400/static/user/{path}";
            }

            // Send message to character
            var response = await integration.CallCharacter(text, imgPath, primaryMsg: _currReplyID);

            // Alert with error message if call returns string
            if (response is string) { await message.ReplyAsync((string)response); return; }

            _lastReplies = response!.replies;
            _lastUserReplyID = response!.last_user_msg_id;
            _currReplyID = null;

            // Take first character answer by default and reply with it
            var reply = _lastReplies[0];
            _lastBotMsgID = await ReplyOnMessage(message, reply);
        }

        private static async Task<string> ReplyOnMessage(SocketUserMessage message, dynamic reply)
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

            
            //await SetArrowButtons(botReply);

            return botReply.Id.ToString();
        }

        private static async Task<IUserMessage?> ReplyWithImage(SocketUserMessage message, byte[] image, string text)
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

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}