using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterAiDiscordBot.Handlers;
using CharacterAiDiscordBot.Models.Common;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace CharacterAiDiscordBot.Services
{
    internal class DiscordService
    {
        public List<string> Prefixes { get; set; } = null!;

        private ServiceProvider _services = null!;
        private DiscordSocketClient _client = null!;
        private IntegrationService _integration = null!;
        private InteractionService _interactions = null!;
        private bool _firstLaunch = true;

        internal async Task SetupDiscordClient()
        {
            _services = CreateServices();

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _integration = _services.GetRequiredService<IntegrationService>();
            _interactions = _services.GetRequiredService<InteractionService>();
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Initialize handlers
            _services.GetRequiredService<ButtonsHandler>();
            _services.GetRequiredService<ReactionsHandler>();
            _services.GetRequiredService<SlashCommandsHandler>();
            _services.GetRequiredService<TextMessagesHandler>();

            BindEvents();
            await SetupIntegrationAsync();

            Prefixes = GetPrefixes();

            await _client.LoginAsync(TokenType.Bot, ConfigFile.DiscordBotToken.Value);
            await _client.StartAsync();
            
            await RunJobsAsync();
        }

        private static string GetLastGameStatus()
        {
            string gamePath = $"{EXE_DIR}{SC}storage{SC}settings{SC}game.txt";
            return File.Exists(gamePath) ? File.ReadAllText(gamePath) : string.Empty;
        }

        private static List<string> GetPrefixes()
        {
            string prefixesPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}prefixes.txt";

            if (File.Exists(prefixesPath))
            {
                string content = File.ReadAllText(prefixesPath);
                if (!string.IsNullOrWhiteSpace(content))
                    return content.Split("\n").OrderBy(p => p.Length).Reverse().ToList(); // ex: "~ai" first, only then "~"
            }

            return new();
        }

        private void BindEvents()
        {
            _client.Log += (msg) =>
            {
                Log($"{msg}\n");
                return Task.CompletedTask;
            };

            _client.Ready += () =>
            {
                Task.Run(async () => await OnClientReadyAsync());
                return Task.CompletedTask;
            };

            _client.JoinedGuild += (guild) =>
            {
                Task.Run(async () => await OnGuildJoinAsync(guild));
                return Task.CompletedTask;
            };

            
            _client.LeftGuild += OnGuildLeft;
        }

        private async Task RunJobsAsync()
        {
            try
            {
                while (true)
                {
                    var db = new StorageContext();

                    var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                    int blockedUsersCount = db.BlockedUsers.Where(bu => bu.GuildId == null).Count();
                    string text = $"Running: `{time.Days}:{time.Hours}:{time.Minutes}`\n" +
                                  $"Blocked: `{blockedUsersCount} user(s)` | `{db.BlockedGuilds.Count()} guild(s)`";

                    var blockedUsersToUnblock = db.BlockedUsers.Where(bu => bu.Hours != 0 && (bu.From.AddHours(bu.Hours) <= DateTime.UtcNow));
                    db.BlockedUsers.RemoveRange(blockedUsersToUnblock);
                    await db.SaveChangesAsync();

                    if (!_firstLaunch)
                    {
                        _integration.WatchDogClear();

                        if (_integration.CaiClient is not null)
                        {
                            _integration.CaiClient.KillBrowser();
                            _integration.CaiClient.LaunchBrowser(killDuplicates: true);
                        }
                    }
                    _firstLaunch = false;

                    TryToReportInLogsChannel(_client, "Status", desc: text, content: null, color: Color.DarkGreen, error: false);
                    await Task.Delay(1_800_000); // 30 min
                }
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                TryToReportInLogsChannel(_client, "Exception", "Jobs", e.ToString(), Color.Red, true);
                _ = Task.Run(RunJobsAsync);
            }
        }

        private async Task OnClientReadyAsync()
        {
            if (!_firstLaunch) return;

            Log($"Registering commands to ({_client.Guilds.Count}) guilds...\n");
            await Parallel.ForEachAsync(_client.Guilds, async (guild, ct) =>
            {
                if (await TryToCreateSlashCommandsAndRoleAsync(guild)) LogGreen(".");
                else LogRed(".");
            });

            LogGreen("\nCommands registered successfully\n");
            TryToReportInLogsChannel(_client, "Notification", "Commands registered successfully\n", null, Color.Green, error: false);

            await _client.SetGameAsync(GetLastGameStatus());
        }

        private async Task OnGuildJoinAsync(SocketGuild guild)
        {
            try
            {
                if ((await new StorageContext().BlockedGuilds.FindAsync(guild.Id)) is not null)
                {
                    await guild.LeaveAsync();
                    return;
                };

                await TryToCreateSlashCommandsAndRoleAsync(guild);
                await TryToSetNicknameAsync(guild);

                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string log = $"Sever name: {guild.Name} ({guild.Id})\n" +
                                $"Owner: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                                $"Members: {guild.MemberCount}\n" +
                                $"{(guild.Description is string desc ? $"Description: \"{desc}\"" : "")}";
                LogGreen(log);
                TryToReportInLogsChannel(_client, "New server", log, null, Color.Green, false);
            }
            catch { return; }
        }

        private Task OnGuildLeft(SocketGuild guild)
        {
            string log = $"Left guild: {guild.Name} | Members: {guild.MemberCount}";
            LogRed(log);
            TryToReportInLogsChannel(_client, "Left server", log, null, Color.Orange, false);

            return Task.CompletedTask;
        }

        internal ServiceProvider CreateServices()
        {
            var discordClient = CreateDiscordClient();
            var services = new ServiceCollection()
                .AddSingleton(discordClient)
                .AddSingleton<SlashCommandsHandler>()
                .AddSingleton<TextMessagesHandler>()
                .AddSingleton<ReactionsHandler>()
                .AddSingleton<ButtonsHandler>()
                .AddSingleton<IntegrationService>()
                .AddSingleton(this)
                .AddSingleton(new InteractionService(discordClient.Rest));

            return services.BuildServiceProvider();
        }

        private async Task SetupIntegrationAsync()
        {
            try {
                await _integration.InitializeAsync();
                if (_integration.SelfCharacter is null) return;
            }
            catch (Exception e) { LogException(new[] { e }); }
        }

        private async Task<bool> TryToCreateSlashCommandsAndRoleAsync(SocketGuild guild)
        {                
            try
            {
                await _interactions.RegisterCommandsToGuildAsync(guild.Id);
                if (!(guild.Roles?.Any(r => r.Name == ConfigFile.DiscordBotRole.Value!) ?? false))
                    await guild.CreateRoleAsync(ConfigFile.DiscordBotRole.Value!, isMentionable: true);

                return true;
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                return false;
            }
        }

        private async Task TryToSetNicknameAsync(SocketGuild guild)
        {
            if (_integration.SelfCharacter is null) return;

            try
            {
                await guild.CurrentUser.ModifyAsync(u => u.Nickname = _integration.SelfCharacter.Name);
            }
            catch (Exception e)
            {
                TryToReportInLogsChannel(_client, "TryToSetNicknameAsync", $"Failed to set bot nickname in guild {guild.Name}", e.ToString(), Color.Red, true);
            }
        }

        private static DiscordSocketClient CreateDiscordClient()
        {
            // Define GatewayIntents
            var intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks;

            // Create client
            var clientConfig = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = intents,
                ConnectionTimeout = 30000,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                AlwaysDownloadDefaultStickers = true,
            };

            return new DiscordSocketClient(clientConfig);
        }
    }
}
