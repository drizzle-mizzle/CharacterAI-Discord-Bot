using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using CharacterAI_Discord_Bot.Service;

namespace CharacterAI_Discord_Bot
{
    public class Program : CommonService
    {
        private ServiceProvider _services;
        private DiscordSocketClient _client;
        static void Main()
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            _services = CreateServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            if (Config is null) return;

            _client.Log += Log;
            _client.Ready += OnClientReady;

            await _client.LoginAsync(TokenType.Bot, Config.botToken);
            await _client.StartAsync();
            await _services.GetRequiredService<MessageHandler>().InitializeAsync();

            await Task.Delay(-1);
        }

        public Task OnClientReady()
        {
            if (Config.autoSetupEnabled)
                Task.Run(() => AutoSetup(_services, _client));

            return Task.CompletedTask;
        }

        private static ServiceProvider CreateServices()
        {
            var clientConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
                ^ GatewayIntents.GuildPresences
                ^ GatewayIntents.GuildScheduledEvents
                ^ GatewayIntents.GuildInvites
            };

            var services = new ServiceCollection();
            services.AddSingleton(new DiscordSocketClient(clientConfig))
                    .AddSingleton(new CommandService())
                    .AddSingleton<MessageHandler>();

            return services.BuildServiceProvider();
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }
    }
}