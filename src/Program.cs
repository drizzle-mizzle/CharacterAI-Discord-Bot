using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using CharacterAI_Discord_Bot.Service;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace CharacterAI_Discord_Bot
{
    public class Program
    {
        private Integration? _integration;
        private DiscordSocketClient? _client;
        private dynamic? _config;
        private bool _audienceMode = false;
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            _config = GetConfig();

            _client = new DiscordSocketClient();
            _client.MessageReceived += MessageHandler;
            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, _config.botToken);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        public async Task<Task> MessageHandler(SocketMessage rawMessage)
        {
            int argPos = 0;
            if (rawMessage.Author.IsBot ||
                !(rawMessage is SocketUserMessage message) ||
                !((message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                  (message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id)))
                return Task.CompletedTask;

            SocketCommandContext context = new(_client, message);
            string text = RemoveMention(context.Message.Content);

            // I swear 
            if (text.StartsWith(".set ") || text.StartsWith(".enable") || text.StartsWith(".disable"))
            {
                if (context.Message.Author.Id != context.Guild.OwnerId)
                {
                    await context.Message.Channel.SendFileAsync(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "nopower.gif");
                    return Task.CompletedTask;
                }

                if (text.StartsWith(".set character "))
                {
                    string charID = text.Replace(".set character", "").Trim(' ');
                    string msg = "⚠️ Failed to set character!";

                    _integration = new Integration();
                    if (_integration.Setup(_config.userToken, charID))
                    {
                        await context.Guild.GetUser(_client.CurrentUser.Id).ModifyAsync(u => { u.Nickname = _integration._charInfo.Name; });
                        await context.Client.SetGameAsync($"Original character:\n https://beta.character.ai/chat?char={_integration._charInfo.CharID}");
                        try
                        {
                            var image = new Discord.Image(new FileStream(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "characterAvatar.avif", FileMode.Open));
                            await context.Client.CurrentUser.ModifyAsync(u => { u.Avatar = image; });
                        }
                        catch { }
                        
                        msg = _integration._charInfo.Greeting;
                    }

                    await context.Message.ReplyAsync(msg);
                }
                // draft
                else if (text.StartsWith(".enable audience")) { _audienceMode = true; await context.Message.ReplyAsync("⚠ Audience mode enabled!"); }
                else if (text.StartsWith(".disable audience")) { _audienceMode = false; await context.Message.ReplyAsync("⚠ Audience mode disabled!"); }

                return Task.CompletedTask;
            }

            if (_integration == null)
            {
                await context.Message.ReplyAsync("⚠ Set a character first");
                return Task.CompletedTask;
            }

            using (context.Channel.EnterTypingState())
            {
                if (_audienceMode)
                {
                    // Test feature that makes character aware that he's talking to many different people
                    string author = context.Message.Author.Username;
                    string? botQuote = null;
                    if (context.Message.ReferencedMessage != null) 
                        botQuote = $"(In response to: \"{RemoveMention(context.Message.ReferencedMessage.Content)}\")\n";

                    text = $"User [{author}] says:\n{text}";

                    if (botQuote != null) text = botQuote + text;
                }
                string reply = _integration.CallCharacter(text);
                await message.ReplyAsync($"{message.Author.Mention} {reply}");
            }

            return Task.CompletedTask;
        }

        private static string RemoveMention(string text)
        {
            var rgx = new Regex(@"\<(.*?)\>");
            text = rgx.Replace(text, "", 1).Trim(' ');

            return text;
        }

        public static dynamic GetConfig()
        {
            using StreamReader configJson = new StreamReader(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Config.json");
            var configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
            dynamic config = new
            {
                userToken = configParsed["char_ai_user_token"].Value<string>(),
                botToken = configParsed["discord_bot_token"].Value<string>()
            };

            return config;
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }
    }
}