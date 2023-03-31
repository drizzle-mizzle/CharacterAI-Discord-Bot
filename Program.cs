using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using CharacterAI_Discord_Bot.Service;
using CharacterAI_Discord_Bot.Handlers;

namespace CharacterAI_Discord_Bot
{
    public class Program : CommandsService
    {
        private ServiceProvider _services = null!;
        private DiscordSocketClient _client = null!;

        static void Main()
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            _services = CreateServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            if (BotConfig is null) return;

            _client.Log += Log;
            _client.Ready += OnClientReady;

            await _client.LoginAsync(TokenType.Bot, BotConfig.BotToken);
            await _client.StartAsync();
            await _services.GetRequiredService<CommandsHandler>().InitializeAsync();

            await Task.Delay(-1);
        }

        public Task OnClientReady()
        {
            var handler = _services.GetRequiredService<CommandsHandler>();

            _ = Task.Run(async () =>
            {
                await handler.CurrentIntegration.LaunchChromeAsync();
                if (BotConfig.AutoSetupEnabled)
                    _ = AutoSetup(handler, _client);
            });
            
            return Task.CompletedTask;
        }

        private static ServiceProvider CreateServices()
        {
            var clientConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
                ^ GatewayIntents.GuildScheduledEvents
                ^ GatewayIntents.GuildInvites,
                MessageCacheSize = 5
            };

            var services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(clientConfig))
                .AddSingleton(new CommandService())
                .AddSingleton<CommandsHandler>();

            return services.BuildServiceProvider();
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private void OnProcessExit(object? sender, EventArgs args)
        {
            Log("Shutting down...");
            try
            {
                var path = _services.GetRequiredService<CommandsHandler>().CurrentIntegration.EXEC_PATH;
                CharacterAI.Integration.KillChromes(path);
            }
            catch (Exception e)
            {
                Failure("Failed to kill Chrome processes.\n" + e.ToString());
            }
        }
    }
}