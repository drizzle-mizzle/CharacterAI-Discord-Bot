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
            _services = CreateServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            if (BotConfig is null) return;

            _client.Log += Log;
            _client.Ready += OnClientReady;
            _client.JoinedGuild += OnGuildJoin;

            await _client.LoginAsync(TokenType.Bot, BotConfig.BotToken);
            await _client.StartAsync();
            await _services.GetRequiredService<CommandsHandler>().InitializeAsync();

            await Task.Delay(-1);
        }

        private async Task OnGuildJoin(SocketGuild guild)
        {
            await CreateBotRoleAsync(guild);
            Success($"Joined guild: {guild.Name} | Owner: {guild.Owner.Username} | Members: {guild.MemberCount}");
        }

        public Task OnClientReady()
        {
            _ = Task.Run(async () => await LaunchChromeAndSetup(setup: BotConfig.AutoSetupEnabled));

            return Task.CompletedTask;
        }

        private async Task LaunchChromeAndSetup(bool setup)
        {
            try
            {
                var handler = _services.GetRequiredService<CommandsHandler>();
                await handler.CurrentIntegration.LaunchChromeAsync(BotConfig.CustomChromePath, BotConfig.CustomChromeExecPath);
                if (setup) await AutoSetup(handler, _client);
            }
            catch (Exception e)
            {
                Failure(e.ToString(), client: _client);
            }
        }

        private static ServiceProvider CreateServices()
        {
            var clientConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
                ^ GatewayIntents.GuildScheduledEvents
                ^ GatewayIntents.GuildInvites
                ^ GatewayIntents.GuildPresences,

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
            if (log.Exception is NullReferenceException) return Task.CompletedTask;

            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }
    }
}