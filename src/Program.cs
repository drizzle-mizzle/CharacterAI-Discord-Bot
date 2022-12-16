using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Windows.Input;

namespace CharacterAI_Discord_Bot
{
    public class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            using var services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig()))
                .AddSingleton(new CommandService())
                .AddSingleton<MessageHandler>()
                .BuildServiceProvider();

            var client = services.GetRequiredService<DiscordSocketClient>();
            var commands = services.GetRequiredService<CommandService>();
            client.Log += Log;

            await client.LoginAsync(TokenType.Bot, GetConfig().botToken);
            await client.StartAsync();
            await services.GetRequiredService<MessageHandler>().InitializeAsync();

            await Task.Delay(-1);
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