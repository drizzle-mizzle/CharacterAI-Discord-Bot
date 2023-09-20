using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterAiDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.IntegrationService;
using static CharacterAiDiscordBot.Services.StorageContext;
using SummaryAttribute = Discord.Interactions.SummaryAttribute;

namespace CharacterAiDiscordBot.Handlers.SlashCommands
{
    [RequireManagerAccess]
    public class ManagerCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationService _integration;
        private readonly StorageContext _db;

        public ManagerCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationService>();
            _db = new StorageContext();
        }

        [SlashCommand("reset", "Forget all history and start chat from the beginning")]
        public async Task ResetCharacter()
        {
            await ResetCharacterAsync();
        }

        [SlashCommand("set-history", "Set chat history by its ID")]
        public async Task SetHistory(string historyId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);

            string message = $"{OK_SIGN_DISCORD} **History ID** for this channel was changed from `{channel.HistoryId}` to `{historyId}`";
            if (historyId.Length != 43)
                message += $".\nEntered history ID has length that is different from expected ({historyId.Length}/43). Make sure it's correct.";

            channel.HistoryId = historyId;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green));
        }

        [SlashCommand("continue-history", "Continue chat history from another channel")]
        public async Task ContinueHistory(SocketGuildChannel channel)
        {
            await DeferAsync();

            var fromChannel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var toChannel = await FindOrStartTrackingChannelAsync(channel.Id, Context.Guild.Id, _db);


            string? newHistoryId = fromChannel.HistoryId;
            if (newHistoryId is null)
            {
                await FollowupAsync(embed: $"No chat history found in {channel.Name}".ToInlineEmbed(Color.Orange));
                return;
            }

            string text = $"{WARN_SIGN_DISCORD} **history_id** for this channel was changed from `{toChannel.HistoryId ?? "not set"}` to `{newHistoryId}`";
            toChannel.HistoryId = newHistoryId;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed(text));
        }

        [SlashCommand("hunt-user", "Make character respond on messages of certain user (or bot)")]
        public async Task HuntUser(IUser? user = null, string? userId = null, float chanceOfResponse = 100)
        {
            await HuntUserAsync(user, userId, chanceOfResponse);
        }

        [SlashCommand("stop-hunt-user", "Stop hunting user")]
        public async Task UnhuntUser(IUser? user = null, string? userId = null)
        {
            await UnhuntUserAsync(user, userId);
        }

        [SlashCommand("set-channel-random-reply-chance", "Set random character replies chance for this channel")]
        public async Task SetChannelRandomReplyChance(float chance)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            string before = channel.RandomReplyChance.ToString();
            channel.RandomReplyChance = chance;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed($"Random reply chance for this channel was changed from {before}% to {chance}%"));
        }

        [SlashCommand("set-channel-response-delay", "Set character response delay for this channel")]
        public async Task SetChannelRandomReplyChance(int seconds)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            string before = channel.ResponseDelay.ToString();
            channel.ResponseDelay = seconds;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed($"Random reply chance for this channel was changed from {before}s to {seconds}s"));
        }

        [SlashCommand("set-channel-messages-format", "Change messages format used in this channel by default")]
        public async Task SetChannelMessagesFormat(string newFormat)
        {
            await DeferAsync();

            if (!await ValidateMessageFormatAsync(newFormat)) return;

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            channel.ChannelMessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            string text = newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer").Replace("{{ref_msg_text}}", "Hola")
                                   .Replace("{{ref_msg_begin}}", "").Replace("{{ref_msg_end}}", "").Replace("{{ref_msg_user}}", "Dude")
                                   .Replace("\\n", "\n");

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Success**").WithColor(Color.Green)
                                          .AddField("New format:", $"`{newFormat}`")
                                          .AddField("Example", $"User message: *`Hello!`*\n" +
                                                               $"User nickname: `Average AI Enjoyer`\n" +
                                                               $"Referenced message: *`Hola`* from user *`Dude`*\n" +
                                                               $"Result (what character will see): *`{text}`*");

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("set-server-messages-format", "Change messages format used on this server by default")]
        public async Task SetServerMessagesFormat(string newFormat)
        {
            await DeferAsync();

            if (!await ValidateMessageFormatAsync(newFormat)) return;

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);
            guild.GuildMessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            string text = newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer").Replace("{{ref_msg_text}}", "Hola")
                                   .Replace("{{ref_msg_begin}}", "").Replace("{{ref_msg_end}}", "").Replace("{{ref_msg_user}}", "Dude")
                                   .Replace("\\n", "\n");

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Success**").WithColor(Color.Green)
                                          .AddField("New format:", $"`{newFormat}`")
                                          .AddField("Example", $"User message: *`Hello!`*\n" +
                                                               $"User nickname: `Average AI Enjoyer`\n" +
                                                               $"Referenced message: *`Hola`* from user *`Dude`*\n" +
                                                               $"Result (what character will see): *`{text}`*");

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("drop-channel-messages-format", "Drop default messages format for this server")]
        public async Task DropChannelMessagesFormat()
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);

            channel.ChannelMessagesFormat = null;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("drop-server-messages-format", "Drop default messages format for this server")]
        public async Task DropGuildMessagesFormat()
        {
            await DeferAsync();

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            guild.GuildMessagesFormat = null;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("say", "Make character say something")]
        public async Task SayAsync(string text)
        { 
            await RespondAsync(text);
        }

        [SlashCommand("block-user", "Make characters ignore certain user on this server.")]
        public async Task ServerBlockUser(IUser? user = null, string? userId = null, [Summary(description: "Don't specify hours to block forever")]int hours = 0)
        {
            await DeferAsync();

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user or user-ID".ToInlineEmbed(Color.Red));
                return;
            }

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id);

            ulong uUserId;
            if (user is null)
            {
                bool ok = ulong.TryParse(userId, out uUserId);

                if (!ok)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                    return;
                }
            }
            else
            {
                uUserId = user!.Id;
            }

            if (guild.BlockedUsers.Any(bu => bu.Id == uUserId))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already blocked".ToInlineEmbed(Color.Red));
                return;
            }

            await _db.BlockedUsers.AddAsync(new() { Id = uUserId, From = DateTime.UtcNow, Hours = hours, GuildId = guild.Id });
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("unblock-user", "-")]
        public async Task ServerUnblockUser(IUser? user = null, string? userId = null)
        {
            await DeferAsync();

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user or user-ID".ToInlineEmbed(Color.Red));
                return;
            }

            ulong uUserId;
            if (user is null)
            {
                bool ok = ulong.TryParse(userId, out uUserId);

                if (!ok)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                    return;
                }
            }
            else
            {
                uUserId = user!.Id;
            }

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id);

            var blockedUser = guild.BlockedUsers.FirstOrDefault(bu => bu.Id == uUserId);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not found".ToInlineEmbed(Color.Red));
                return;
            }

            _db.BlockedUsers.Remove(blockedUser);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task<bool> ValidateMessageFormatAsync(string newFormat)
        {
            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't set format without a **`{{{{msg}}}}`** placeholder!".ToInlineEmbed(Color.Red));
                return false;
            }

            int refCount = 0;
            if (newFormat.Contains("{{ref_msg_begin}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_text}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_end}}")) refCount++;

            if (refCount != 0 && refCount != 3)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong `ref_msg` placeholder format!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }

        private async Task ResetCharacterAsync()
        {
            await DeferAsync();

            var character = _integration.SelfCharacter;
            if (character is null)
            {
                await FollowupAsync(embed: $"Character is not set".ToInlineEmbed(Color.Orange));
                return;
            }

            var newHistoryIdTask = _integration.CaiClient.CreateNewChatAsync(character.Id, _integration.CaiAuthToken, _integration.CaiPlusMode);
            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);

            string? newHistoryId = await newHistoryIdTask;
            if (newHistoryId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch new history ID".ToInlineEmbed(Color.Red));
                return;
            }

            channel.HistoryId = await newHistoryIdTask;
            await _db.SaveChangesAsync();

            await FollowupAsync(character.Greeting);
        }

        private async Task HuntUserAsync(IUser? user, string? userId, float chanceOfResponse)
        {
            await DeferAsync();

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not specified".ToInlineEmbed(Color.Red));
                return;
            }

            ulong? userToHuntId = user?.Id;

            if (userToHuntId is null)
            {
                bool isId = ulong.TryParse(userId!.Trim(), out ulong ulongId);
                if (isId) userToHuntId = ulongId;
            }

            if (userToHuntId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User was not found".ToInlineEmbed(Color.Red));
                return;
            }

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            if (guild.HuntedUsers.Any(h => h.UserId == userToHuntId))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already hunted".ToInlineEmbed(Color.Orange));
                return;
            }

            await _db.HuntedUsers.AddAsync(new() { UserId = (ulong)userToHuntId, Chance = chanceOfResponse, GuildId = guild.Id });
            await _db.SaveChangesAsync();

            string username = user?.Mention ?? userId!;
            await FollowupAsync(embed: $":ghost: Hunting **{username}**".ToInlineEmbed(Color.LighterGrey, false));
        }

        private async Task UnhuntUserAsync(IUser? user, string? userId)
        {
            await DeferAsync();

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not specified".ToInlineEmbed(Color.Red));
                return;
            }

            ulong? userToUnhuntId = user?.Id;

            if (userToUnhuntId is null)
            {
                bool isId = ulong.TryParse(userId!.Trim(), out ulong ulongId);
                if (isId) userToUnhuntId = ulongId;
            }

            if (userToUnhuntId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User was not found".ToInlineEmbed(Color.Red));
                return;
            }

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);
            var userToUnhunt = guild.HuntedUsers.FirstOrDefault(h => h.UserId == userToUnhuntId);

            if (userToUnhunt is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is not hunted".ToInlineEmbed(Color.Orange));
                return;
            }

            _db.HuntedUsers.Remove(userToUnhunt);
            await _db.SaveChangesAsync();

            string username = user?.Mention ?? userId!;

            await FollowupAsync(embed: SuccessEmbed());
        }

    }
}
