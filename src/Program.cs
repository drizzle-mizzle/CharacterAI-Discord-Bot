using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using static CharacterAI_Discord_Bot.Integration;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace CharacterAI_Discord_Bot
{
    public class Program
    {
        public Integration _bot;
        public DiscordSocketClient _client;
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            
            dynamic config = GetConfig();

            _bot = new Integration();
            _bot.Setup(config.charId, config.userToken);

            _client = new DiscordSocketClient();
            _client.MessageReceived += MessageHandler;
            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, config.botToken);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private Task MessageHandler(SocketMessage rawMessage)
        {
            int argPos = 0;
            if (rawMessage.Author.IsBot ||
                !(rawMessage is SocketUserMessage message) ||
                !(
                    (message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                    (message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id)
                 ))
                return Task.CompletedTask;

            string text = RemoveMention(message.Content);
            string reply = _bot.CallCharacter(text);
            message.Channel.SendMessageAsync($"{message.Author.Mention} {reply}");

            return Task.CompletedTask;
        }

        public static dynamic GetConfig()
        {
            using StreamReader configJson = new StreamReader(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Config.json");
            var configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
            string charId = configParsed["character_id"].Value<string>();
            string userToken = configParsed["char_ai_user_token"].Value<string>();
            string botToken = configParsed["discord_bot_token"].Value<string>();

            return new { charId = charId, userToken = userToken, botToken = botToken };
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private static string RemoveMention(string text)
        {
            var rgx = new Regex(@"\<(.*?)\>");
            text = rgx.Replace(text, "", 1).Trim(' ');

            return text;
        }
    }
}