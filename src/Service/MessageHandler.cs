using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;


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
            integration = new Integration(Program.GetConfig().userToken);

            _client.MessageReceived += HandleMessageAsync;
        }

        public async Task HandleMessageAsync(SocketMessage rawMessage)
        {
            int argPos = 0;
            var message = rawMessage as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            string[] prefixes = GetConfig().botPrefixes;

            // Return if no mention, no prefix and no reply
            if (!message.HasMentionPrefix(_client.CurrentUser, ref argPos) &&
                !prefixes.Any(p => message.HasStringPrefix(p, ref argPos)) &&
                !(message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id))
                return;

            // Execute and return if message was a command
            var cmdResponse = await _commands.ExecuteAsync(context, argPos, _services);
            if (cmdResponse.IsSuccess && (cmdResponse.ErrorReason == null || cmdResponse.ErrorReason != "Unknown Command." )) return;

            // Return if try to call a character before it was set up
            if (integration.charInfo.CharID == null) { await message.ReplyAsync("⚠ Set a character first"); return; }

            // Calling character
            using (message.Channel.EnterTypingState())
            {
                string text = RemoveMention(message.Content);
                string imgPath = "";
                if (message.Attachments.Any())
                {
                    byte[]? img = DownloadImg(message.Attachments.First().Url);
                    imgPath = $"https://characterai.io/i/400/static/user/{integration.UploadImg(img)}";
                }

                if (integration.audienceMode)
                {
                    // Test feature that makes character aware that he's talking to many different people
                    string author = message.Author.Username;
                    if (!string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(text)) 
                        text = $"User [{author}] says:\n{text}";
                    if (message.ReferencedMessage != null)
                        text = $"(In response to: \"{RemoveMention(message.ReferencedMessage.Content)}\")\n{text}";
                }

                string[] reply = integration.CallCharacter(text, imgPath);

                // If no attachments
                if (string.IsNullOrEmpty(reply[1]))
                { 
                    // Simple reply
                    await message.ReplyAsync(reply[0]);

                    return;
                }
                // If has attachments
                byte[] image = DownloadImg(reply[1]);
                using Stream mc = new MemoryStream(image);
                if (File.Exists(tempImgPath)) File.Delete(tempImgPath);

                using var tempImg = File.Create(tempImgPath);
                mc.CopyTo(tempImg);
                tempImg.Close();

                // Reply with attachment
                var mRef = new MessageReference(messageId: message.Id);
                await message.Channel.SendFileAsync(tempImgPath, reply[0], messageReference: mRef);
            }

            return;
        }

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}