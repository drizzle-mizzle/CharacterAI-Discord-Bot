using Discord;
using Discord.Interactions;
using CharacterAiDiscordBot.Services;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.CommandsService;
using static CharacterAiDiscordBot.Services.IntegrationService;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using System.Diagnostics;

namespace CharacterAiDiscordBot.Handlers.SlashCommands
{
    [RequireHosterAccess]
    [Group("admin", "Admin commands")]
    public class AdminCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationService _integration;
        private readonly DiscordService _discordService;
        private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public AdminCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationService>();
            _discordService = services.GetRequiredService<DiscordService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = new StorageContext();
        }

        [SlashCommand("add-call-prefix", "-")]
        public async Task AddPrefix(string prefix)
        {
            await DeferAsync();

            string prefixesPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}prefixes.txt";
            _discordService.Prefixes.Add(prefix);

            File.WriteAllText(prefixesPath, string.Join("\n", _discordService.Prefixes));

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("remove-call-prefix", "-")]
        public async Task RemovePrefix(string prefix)
        {
            await DeferAsync();

            string prefixesPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}prefixes.txt";
            _discordService.Prefixes.Remove(prefix);

            File.WriteAllText(prefixesPath, string.Join("\n", _discordService.Prefixes));

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("setup", "-")]
        public async Task SetupIntegration(string characterAiAuthToken, bool plusMode)
        {
            await DeferAsync(ephemeral: true);

            string authPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}auth.txt";
            File.WriteAllText(authPath, $"{characterAiAuthToken}:{plusMode}");

            _integration.CaiAuthToken = characterAiAuthToken;
            _integration.CaiPlusMode = plusMode;

            await FollowupAsync(ephemeral: true, embed: SuccessEmbed());
        }

        [SlashCommand("set-character", "-")]
        public async Task SetCharacter(string characterNameOrId, bool setWithId = false)
        {
            await DeferAsync();

            if (_integration.CaiAuthToken is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} CharacterAI auth token is not set".ToInlineEmbed(Color.Red));
                return;
            }

            await FollowupAsync(embed: WAIT_MESSAGE);

            if (setWithId)
                await TryToSetCharacterWithIdAsync(characterNameOrId);
            else
                await TryToFindCharacterAsync(characterNameOrId);
        }

        [SlashCommand("status", "-")]
        public async Task AdminStatus()
        {
            await DeferAsync();
            var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            string text = $"Running: `{time.Days}d/{time.Hours}h/{time.Minutes}m`\n" +
                          $"Blocked: `{_db.BlockedUsers.Where(bu => bu.GuildId == null).Count()} user(s)` | `{_db.BlockedGuilds.Count()} guild(s)`";

            await FollowupAsync(embed: text.ToInlineEmbed(Color.Green, false));
        }

        [SlashCommand("list-servers", "-")]
        public async Task AdminListServers(int page = 1)
        {
            await ListServersAsync(page);
        }

        [SlashCommand("leave-all-servers", "-")]
        public async Task AdminLeaveAllGuilds()
        {
            await DeferAsync();

            if (Context.Guild is null)
            {
                await FollowupAsync("This command can't be used in DM");
                return;
            }

            var guilds = _client.Guilds.Where(g => g.Id != Context.Guild.Id);
            await Parallel.ForEachAsync(guilds, async (guild, ct) => await guild.LeaveAsync());

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("block-server", "-")]
        public async Task AdminBlockGuild(string serverId)
        {
            await BlockGuildAsync(serverId);
        }

        [SlashCommand("unblock-server", "-")]
        public async Task AdminUnblockGuild(string serverId)
        {
            await DeferAsync();

            var blockedGuild = await _db.BlockedGuilds.FindAsync(ulong.Parse(serverId.Trim()));

            if (blockedGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server not found".ToInlineEmbed(Color.Red));
                return;
            }

            _db.BlockedGuilds.Remove(blockedGuild);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("block-user-global", "-")]
        public async Task AdminBlockUser(string userId)
        {
            await DeferAsync();

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                return;
            }

            if ((await _db.BlockedUsers.FindAsync(uUserId)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already blocked".ToInlineEmbed(Color.Red));
                return;
            }

            await _db.BlockedUsers.AddAsync(new() { Id = uUserId, From = DateTime.UtcNow, Hours = 0 });
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("unblock-user-global", "-")]
        public async Task AdminUnblockUser(string userId)
        {
            await DeferAsync();

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                return;
            }

            var blockedUser = await _db.BlockedUsers.FindAsync(uUserId);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not found".ToInlineEmbed(Color.Red));
                return;
            }

            _db.BlockedUsers.Remove(blockedUser);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("broadcast", "Send a message in each channel where bot was ever called")]
        public async Task AdminShoutOut(string title, string? desc = null, string? imageUrl = null)
        {
            await DeferAsync();

            var embedB = new EmbedBuilder().WithColor(Color.Orange);
            if (title is not null) embedB.WithTitle(title);
            if (desc is not null) embedB.WithDescription(desc);
            if (imageUrl is not null) embedB.WithImageUrl(imageUrl);
            var embed = embedB.Build();

            var channelIds = _db.Channels.Select(c => c.Id).ToList();
            var channels = new List<IMessageChannel>();

            await Parallel.ForEachAsync(channelIds, async (channelId, ct) =>
            {
                IMessageChannel? mc;
                try { mc = (await _client.GetChannelAsync(channelId)) as IMessageChannel; }
                catch { return; }
                if (mc is not null) channels.Add(mc);
            });

            int count = 0;
            await Parallel.ForEachAsync(channels, async (channel, ct) =>
            {
                try {
                    await channel.SendMessageAsync(embed: embed);
                    count++;
                }
                catch { return; }
            });
                
            await FollowupAsync(embed: SuccessEmbed($"Message was sent in {count} channels"), ephemeral: true);
        }

        [SlashCommand("server-stats", "-")]
        public async Task AdminGuildStats(string? guildId = null)
        {
            await DeferAsync();

            ulong uGuildId;
            if (guildId is null)
            {
                if (Context.Guild is null)
                {
                    await FollowupAsync("This command can't be used in DM");
                    return;
                }

                uGuildId = Context.Guild.Id;
            }
            else
            {
                if (!ulong.TryParse(guildId, out uGuildId))
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong ID".ToInlineEmbed(Color.Red));
                    return;
                }
            }
            
            var guild = _client.GetGuild(uGuildId);
            if (guild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Guild not found".ToInlineEmbed(Color.Red));
                return;
            }


            var allChannels = new StorageContext().Channels.Where(c => c.GuildId == guild.Id);
            var lastUsed = allChannels.OrderByDescending(c => c.LastCallTime)?.FirstOrDefault()?.LastCallTime;
            string callDate = lastUsed is null ? "?" : $"{lastUsed.Value.Day}/{lastUsed.Value.Month}/{lastUsed.Value.Year}";
            ulong messagesSent = 0;
            foreach (var ch in allChannels)
                messagesSent += ch.MessagesSent;

            string desc = $"**Owner:** `{guild.Owner.Username}`\n" +
                          $"**Last character call:** `{callDate}`\n" +
                          $"**Messages sent:** `{messagesSent}`";

            var embed = new EmbedBuilder().WithTitle(guild.Name)
                                          .WithColor(Color.Magenta)
                                          .WithDescription(desc);
            if (guild.IconUrl is not null) embed.WithImageUrl(guild.IconUrl);

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("shutdown", "Shutdown")]
        public async Task AdminShutdownAsync()
        {
            await DeferAsync();

            if (_integration.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Puppeteer is not launched".ToInlineEmbed(Color.Red));
                return;
            }

            await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Shutting down...".ToInlineEmbed(Color.Orange));

            try { _integration.CaiClient.KillBrowser(); }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to kill Puppeteer processes".ToInlineEmbed(Color.Red));
                LogException(new[] { "Failed to kill Puppeteer processes.\n", e.ToString() });
                return;
            }

            Environment.Exit(0);
        }

        [SlashCommand("relaunch-puppeteer", "-")]
        public async Task RelaunchBrowser()
        {
            await DeferAsync();

            if (_integration.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Puppeteer is not launched".ToInlineEmbed(Color.Red));
                return;
            }

            await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Shutting Puppeteer down...".ToInlineEmbed(Color.LightOrange));

            try { _integration.CaiClient.KillBrowser(); }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to kill Puppeteer processes".ToInlineEmbed(Color.Red));
                LogException(new[] { "Failed to kill Puppeteer processes.\n", e.ToString() });
                return;
            }

            await FollowupAsync(embed: "Launching Puppeteer...".ToInlineEmbed(Color.Purple));

            try { _integration.CaiClient.LaunchBrowser(killDuplicates: true); }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to launch Puppeteer processes".ToInlineEmbed(Color.Red));
                LogException(new[] { "Failed to launch Puppeteer processes.\n", e.ToString() });
                return;
            }

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-game", "Set game status")]
        public async Task AdminUpdateGame(string? activity = null, string? streamUrl = null, ActivityType type = ActivityType.Playing)
        {
            await _client.SetGameAsync(activity, streamUrl, type);
            await RespondAsync(embed: SuccessEmbed());

            string gamePath = $"{EXE_DIR}{SC}storage{SC}settings{SC}game.txt";
            File.WriteAllText(gamePath, activity ?? "");
        }

        [SlashCommand("set-status", "Set status")]
        public async Task AdminUpdateStatus(UserStatus status)
        {
            await _client.SetStatusAsync(status);
            await RespondAsync(embed: SuccessEmbed());
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task TryToSetCharacterWithIdAsync(string characterId)
        {
            var character = await _integration.CaiClient.GetInfoAsync(characterId, _integration.CaiAuthToken, _integration.CaiPlusMode);

            if (character is null || character.IsEmpty)
            {
                await ModifyOriginalResponseAsync(r => r.Embed = $"{WARN_SIGN_DISCORD} Failed to set a character".ToInlineEmbed(Color.Red));
                return;
            }

            _integration.SelfCharacter = new()
            {
                Id = characterId,
                Name = character.Name ?? "character",
                AuthorName = character.Author ?? "unknown",
                AvatarUrl = character.AvatarUrlFull ?? character.AvatarUrlMini,
                Title = character.Title,
                Description = character.Description ?? "No description",
                Greeting = character.Greeting ?? $"*{character.Name} has joined the chat*",
                ImageGenEnabled = character.ImageGenEnabled ?? false,
                Interactions = character.Interactions ?? 0,
                Tgt = character.Tgt!,
            };

            await ModifyOriginalResponseAsync(msg => msg.Embed = CharacterInfoEmbed(_integration.SelfCharacter));

            string lastCharacterIdPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}last_character.txt";
            File.WriteAllText(lastCharacterIdPath, characterId);

            await TryToSetCharacterAvatarAsync(_integration.SelfCharacter, _client.CurrentUser, _integration.HttpClient);
            await Context.Channel.SendMessageAsync(_integration.SelfCharacter.Greeting);

            await Parallel.ForEachAsync(_client.Guilds, async (guild, ct) =>
            {
                try { await guild.CurrentUser.ModifyAsync(u => u.Nickname = character.Name); }
                catch { return; }
            });
        }

        private async Task TryToFindCharacterAsync(string characterName)
        {
            var response = await _integration.CaiClient.SearchAsync(characterName, _integration.CaiAuthToken, _integration.CaiPlusMode);
            var searchQueryData = SearchQueryDataFromCaiResponse(response);

            var newSQ = await BuildAndSendSelectionMenuAsync(Context, searchQueryData);
            if (newSQ is null) return;

            _integration.LastSearchQuery = newSQ;
        }

        private async Task ListServersAsync(int page)
        {
            await DeferAsync();

            var embed = new EmbedBuilder().WithColor(Color.Green);

            int start = (page - 1) * 10;
            int end = (_client.Guilds.Count - start) > 10 ? (start + 9) : start + (_client.Guilds.Count - start - 1);

            var guilds = _client.Guilds.OrderBy(g => g.MemberCount).Reverse();

            for (int i = start; i <= end; i++)
            {
                var guild = guilds.ElementAt(i);
                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string val = $"ID: {guild.Id}\n" +
                             $"{(guild.Description is string desc ? $"Description: \"{desc[0..Math.Min(200, desc.Length-1)]}\"\n" : "")}" +
                             $"Owner: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                             $"Members: {guild.MemberCount}";
                embed.AddField(guild.Name, val);
            }
            double pages = Math.Ceiling(_client.Guilds.Count / 10d);
            embed.WithTitle($"Servers: {_client.Guilds.Count}");
            embed.WithFooter($"Page {page}/{pages}");

            await FollowupAsync(embed: embed.Build());
        }
        private async Task BlockGuildAsync(string serverId)
        {
            await DeferAsync();

            ulong guildId = ulong.Parse(serverId.Trim());
            var guild = await _db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server not found".ToInlineEmbed(Color.Red));
                return;
            }

            if ((await _db.BlockedGuilds.FindAsync(guild.Id)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server is aready blocked".ToInlineEmbed(Color.Orange));
                return;
            }

            await _db.BlockedGuilds.AddAsync(new() { Id = guildId });
            _db.Guilds.Remove(guild); // Remove from db
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server was removed from the database".ToInlineEmbed(Color.Red));

            // Leave
            var discordGuild = _client.GetGuild(guildId);

            if (discordGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to leave the server".ToInlineEmbed(Color.Red));
                return;
            }

            await discordGuild.LeaveAsync();
            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server \"{discordGuild.Name}\" is leaved".ToInlineEmbed(Color.Red));
        }
    }
}
