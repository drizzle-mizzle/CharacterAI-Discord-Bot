using Discord;
using Discord.WebSocket;
using CharacterAI;
using CharacterAiDiscordBot.Models.Common;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.CommandsService;
using Discord.Commands;

namespace CharacterAiDiscordBot.Services
{
    public class IntegrationService
    {
        /// <summary>
        /// (User ID : [current minute : interactions count])
        /// </summary>
        private readonly Dictionary<ulong, KeyValuePair<int, int>> _watchDog = new();

        internal HttpClient HttpClient { get; } = new();
        internal CharacterAIClient CaiClient { get; set; } = null!;
        internal Character? SelfCharacter { get; set; }
        internal SearchQuery? LastSearchQuery { get; set; }

        internal string? CaiAuthToken { get; set; }
        internal bool CaiPlusMode { get; set; } = false;

        /// <summary>
        /// Stored swiped messages (History ID : AvailableCharacterResponse)
        /// </summary>
        internal Dictionary<string, List<AvailableCharacterResponse>> AvailableCharacterResponses { get; } = new();

        public async Task InitializeAsync()
        {
            HttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            HttpClient.DefaultRequestHeaders.Add("AcceptEncoding", "gzip, deflate, br");
            HttpClient.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36");

            CaiClient = new(
                customBrowserDirectory: ConfigFile.PuppeteerBrowserDir.Value,
                customBrowserExecutablePath: ConfigFile.PuppeteerBrowserExe.Value
            );

            CaiClient.LaunchBrowser(killDuplicates: true);
            AppDomain.CurrentDomain.ProcessExit += (s, args) => CaiClient.KillBrowser();

            Log("CharacterAI client - "); LogGreen("Running\n\n");

            string authPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}auth.txt";
            if (File.Exists(authPath))
            {
                string content = File.ReadAllText(authPath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    CaiAuthToken = content.Split(':').First();
                    CaiPlusMode = content.Split(':').Last().ToBool();
                }
            }

            string? characterId = null;
            string lastCharacterIdPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}last_character.txt";

            characterId = File.ReadAllText(lastCharacterIdPath);

            if (string.IsNullOrWhiteSpace(characterId))
            {
                LogYellow("No character selected\n\n");
            }
            else
            {
                Log("Fetching character info... ");
                var character = await CaiClient.GetInfoAsync(characterId, CaiAuthToken, CaiPlusMode);
                if (character.IsEmpty)
                {
                    LogRed("fail\n");
                    LogYellow("No character selected\n\n");
                }
                else
                {
                    LogGreen("OK\n");

                    SelfCharacter = new()
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

                    Log($"{SelfCharacter.Name}\n\"{SelfCharacter.Title}\"\n\n{SelfCharacter.Description}\n\n");
                }
            }
        }

        internal async Task<CharacterResponse> CallCaiCharacterAsync(string text, string historyId, string? primaryMsgId, string? parentMsgId)
        {
            var caiResponse = await CaiClient.CallCharacterAsync(SelfCharacter!.Id, SelfCharacter.Tgt, historyId, text, null, primaryMsgId, parentMsgId, CaiAuthToken, CaiPlusMode);

            string message;
            bool success = false;

            if (caiResponse.IsSuccessful)
            {
                message = caiResponse.Response!.Text;
                success = true;
            }
            else
            {
                message = $"{WARN_SIGN_DISCORD} Failed to fetch character response: ```\n{caiResponse.ErrorReason}\n```";
            }

            return new()
            {
                Text = message,
                IsSuccessful = success,
                CharacterMessageId = caiResponse.Response?.UuId,
                ImageRelPath = caiResponse.Response?.ImageRelPath,
                UserMessageId = caiResponse.LastUserMsgUuId
            };
        }


        internal static SearchQueryData SearchQueryDataFromCaiResponse(CharacterAI.Models.SearchResponse response)
        {
            var characters = new List<Models.Common.Character>();

            foreach(var character in response.Characters)
            {
                if (character.IsEmpty) continue;
                characters.Add(new ()
                {
                    Id = character.Id!,
                    Name = character.Name ?? "character",
                    AuthorName = character.Author ?? "unknown",
                    AvatarUrl = character.AvatarUrlFull ?? character.AvatarUrlMini,
                    Title = character.Title,
                    Description = character.Description ?? "No description",
                    Greeting = character.Greeting ?? $"*{character.Name} has joined the chat*",
                    ImageGenEnabled = character.ImageGenEnabled ?? false,
                    Interactions = character.Interactions ?? 0,
                    Tgt = character.Tgt!,
                });
            }

            return new(characters, response.OriginalQuery) { ErrorReason = response.ErrorReason };
        }

        internal static async Task<bool> UserIsBannedCheckOnly(ulong userId)
            => (await new StorageContext().BlockedUsers.FindAsync(userId)) is not null;

        internal async Task<bool> UserIsBanned(SocketCommandContext context)
        {
            var user = context.Message.Author;
            var channel = context.Channel;

            return await CheckIfUserIsBannedAsync(user, channel, context.Client);
        }

        internal async Task<bool> UserIsBanned(SocketReaction reaction, DiscordSocketClient client)
        {
            var user = reaction.User.GetValueOrDefault();
            var channel = reaction.Channel;
            if (user is null) return true;

            return await CheckIfUserIsBannedAsync(user, channel, client);
        }

        internal async Task<bool> CheckIfUserIsBannedAsync(IUser user, ISocketMessageChannel channel, DiscordSocketClient client)
        {
            var db = new StorageContext();
            
            var blockedUser = await db.BlockedUsers.FindAsync(user.Id);
            if (blockedUser is not null) return true;

            int currentMinuteOfDay = DateTime.UtcNow.Minute + DateTime.UtcNow.Hour * 60;

            // Start watching for user
            if (!_watchDog.ContainsKey(user.Id))
                _watchDog.Add(user.Id, new(-1, 0)); // user id : (current minute : count)

            // Drop + update user stats if he replies in another minute
            if (_watchDog[user.Id].Key != currentMinuteOfDay)
                _watchDog[user.Id] = new(currentMinuteOfDay, 0);

            // Update interactions count within current minute
            _watchDog[user.Id] = new(_watchDog[user.Id].Key, _watchDog[user.Id].Value + 1);

            int rateLimit = int.Parse(ConfigFile.RateLimit.Value!);

            if (_watchDog[user.Id].Value == rateLimit - 2)
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} {MentionUtils.MentionUser(user.Id)} Warning! If you proceed to call the bot so fast, you'll be blocked from using it.".ToInlineEmbed(Color.Orange));
                return false;
            }

            if (_watchDog[user.Id].Value <= rateLimit)
            {
                return false;
            }

            await db.BlockedUsers.AddAsync(new() { Id = user.Id, From = DateTime.UtcNow, Hours = 24 });
            await db.SaveChangesAsync();

            _watchDog.Remove(user.Id);

            var textChannel = await client.GetChannelAsync(channel.Id) as SocketTextChannel;
            await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} {user.Mention}, you were calling the characters way too fast and have exceeded the rate limit.\nYou will not be able to use the bot in next 24 hours.".ToInlineEmbed(Color.Red));

            TryToReportInLogsChannel(client, title: $":eyes: Notification",
                                             desc: $"Server: **{textChannel?.Guild.Name} ({textChannel?.Guild.Id})** owned by **{textChannel?.Guild.Owner.Username} ({textChannel?.Guild.OwnerId})**\n" +
                                                   $"User **{user.Username} ({user.Id})** hit the rate limit and was blocked",
                                             content: null,
                                             color: Color.LightOrange,
                                             error: false);

            return true;
        }

        public void WatchDogClear()
        {
            _watchDog.Clear();
        }

        // Shortcuts
        internal static Embed SuccessEmbed(string message = "Success", string? imageUrl = null)
            => $"{OK_SIGN_DISCORD} {message}".ToInlineEmbed(Color.Green, imageUrl: imageUrl);
    }
}
