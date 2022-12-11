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

            JObject config = GetConfig();
            string charAiUserToken = config["char_ai_user_token"].Value<string>();
            string characterId = config["character_id"].Value<string>();
            string discordBotToken = config["discord_bot_token"].Value<string>();

            _bot = new Integration();
            _bot.Setup(characterId, charAiUserToken);

            _client = new DiscordSocketClient();
            _client.MessageReceived += MessageHandler;
            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, discordBotToken);
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
                    (message.ReferencedMessage != null && message.ReferencedMessage.Author.IsBot)
                 ))
                return Task.CompletedTask;

            string text = RemoveMention(message.Content);
            string reply = _bot.CallCharacter(text);
            message.Channel.SendMessageAsync($"{message.Author.Mention} {reply}");

            return Task.CompletedTask;
        }
        private string RemoveMention(string text)
        {
            var rgx = new Regex(@"\<(.*?)\>");
            text = rgx.Replace(text, "", 1).Trim(' ');

            return text;
        }
        public static JObject GetConfig()
        {
            using StreamReader configJson = new StreamReader(Directory.GetCurrentDirectory() + @"\Config.json");

            return (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }
    }
}