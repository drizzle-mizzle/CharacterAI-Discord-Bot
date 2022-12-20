using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using CharacterAI_Discord_Bot.Service;
using System.Reflection.Metadata;

namespace CharacterAI_Discord_Bot
{
    public class Program : CommonService
    {
        static void Main()
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            var clientConfig = new DiscordSocketConfig() {
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.All
            };

            using var services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(clientConfig))
                .AddSingleton(new CommandService())
                .AddSingleton<MessageHandler>()
                .BuildServiceProvider();

            var client = services.GetRequiredService<DiscordSocketClient>();
            client.Log += Log;
            dynamic config = GetConfig();

            await client.LoginAsync(TokenType.Bot, config.botToken);
            await client.StartAsync();
            await services.GetRequiredService<MessageHandler>().InitializeAsync();


            if (config.autoSetup) await AutoSetup(services, client);

            await Task.Delay(-1);
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }
    }
}