using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;


namespace CharacterAI_Discord_Bot.Service
{
    public class MessageHandler : CommonService
    {
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;
        public readonly Integration integration;

        public MessageHandler(IServiceProvider services)
        {
            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            integration = new Integration(Config.userToken);

            _client.MessageReceived += HandleMessageAsync;
        }

        public async Task HandleMessageAsync(SocketMessage rawMessage)
        {
            int argPos = 0;
            var message = rawMessage as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            string[] prefixes = Config.botPrefixes;

            // Return if no mention, no prefix and no reply
            if (!message.HasMentionPrefix(_client.CurrentUser, ref argPos) &&
                !prefixes.Any(p => message.HasStringPrefix(p, ref argPos)) &&
                !(message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id))
                return;

            // Execute and return if message was a command
            var cmdResponse = await _commands.ExecuteAsync(context, argPos, _services);
            if (cmdResponse.IsSuccess) return;

            // Return if try to call a character before it was set up
            if (integration.charInfo.CharID == null) { await message.ReplyAsync("⚠ Set a character first"); return; }

            // Calling character
            using (message.Channel.EnterTypingState())
                Task.Run(() => { CallCharacterAsync(message); } ).Wait();

            return;
        }

        private async Task CallCharacterAsync(SocketUserMessage message)
        {
            string text = RemoveMention(message.Content);
            string imgPath = "";
            if (message.Attachments.Any())
            {   // Gets first image from attachments and uploads it to server
                string url = message.Attachments.First().Url;
                if ((await DownloadImg(url) is byte[] img) && integration.UploadImg(img) is Task<string> path)
                    imgPath = $"https://characterai.io/i/400/static/user/{path}";
            }

            if (integration.audienceMode)
                text = MakeItThreadMessage(text, message);

            string[] reply = await integration.CallCharacter(text, imgPath);
            byte[]? image = await DownloadImg(reply[1]);

            if (string.IsNullOrEmpty(reply[1]) || image is null)
                // If no attachments
                await message.ReplyAsync(reply[0]);
            else // If has attachments
                await ReplyWithImage(message, image, text: reply[0]);
        }

        private static async Task ReplyWithImage(SocketUserMessage message, byte[] image, string text)
        {
            using Stream mc = new MemoryStream(image);
            try
            {
                if (File.Exists(tempImgPath)) File.Delete(tempImgPath);
                using var file = File.Create(tempImgPath);
                mc.CopyTo(file);
            }
            catch (Exception e) { Failure("Something went wrong...\n" + e.ToString()); return; }

            var mRef = new MessageReference(messageId: message.Id);
            await message.Channel.SendFileAsync(tempImgPath, text, messageReference: mRef);
        }

        // Test feature that makes character aware that he's talking to many different people
        private static string MakeItThreadMessage(string text, SocketUserMessage message)
        {
            string author = message.Author.Username;
            if (!string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(text))
                text = $"User [{author}] says:\n{text}";
            if (message.ReferencedMessage != null)
                text = $"(In response to: \"{RemoveMention(message.ReferencedMessage.Content)}\")\n{text}";

            return text;
        }

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}