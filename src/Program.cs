using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Windows.Input;
using CharacterAI_Discord_Bot.Service;

namespace CharacterAI_Discord_Bot
{
    public class Program : CommonService
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
            client.Log += Log;

            await client.LoginAsync(TokenType.Bot, GetConfig().botToken);
            await client.StartAsync();
            await services.GetRequiredService<MessageHandler>().InitializeAsync();

            await Task.Delay(-1);
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }
    }
}