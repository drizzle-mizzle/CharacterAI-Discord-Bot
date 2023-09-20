using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using CharacterAiDiscordBot.Services;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.CommandsService;
using static CharacterAiDiscordBot.Services.StorageContext;
using CharacterAiDiscordBot.Models.Database;
using Microsoft.Extensions.DependencyInjection;
using CharacterAiDiscordBot.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace CharacterAiDiscordBot.Handlers
{
    internal class TextMessagesHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationService _integration;
        private readonly DiscordService _discordService;

        public TextMessagesHandler(IServiceProvider services)
        {
            _services = services;
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _integration = _services.GetRequiredService<IntegrationService>();
            _discordService = _services.GetRequiredService<DiscordService>();

            _client.MessageReceived += (message) =>
            {
                Task.Run(async () => {
                    try { await HandleMessageAsync(message); }
                    catch (Exception e) { HandleTextMessageException(message, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleMessageAsync(SocketMessage sm)
        {
            if (_integration.SelfCharacter is null) return;
            if (sm is not SocketUserMessage msg) return;

            bool isMyMessage = Equals(msg.Author.Id, _client.CurrentUser.Id);
            if (isMyMessage) return;

            bool ignore = msg.Content.StartsWith("~ignore");
            if (ignore) return;

            int argPos = 0;
            var db = new StorageContext();

            var context = new SocketCommandContext(_client, msg);
            var channel = await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild?.Id, db);

            bool gottaReply;
            gottaReply = context.Guild is null || (msg.ReferencedMessage is IUserMessage refm && refm.Author.Id == _client.CurrentUser.Id) ||
                         msg.HasMentionPrefix(_client.CurrentUser, ref argPos) || _discordService.Prefixes.Any(p => msg.HasStringPrefix(p, ref argPos)) ||
                         IsRandomReply(channel) || UserIsHunted(channel, msg.Author.Id);

            if (gottaReply)
            {
                var typing = context.Channel.EnterTypingState();
                try
                {
                    await TryToCallCharacterAsync(context, channel, db);
                }
                finally
                {
                    typing.Dispose();
                }
            }
        }

        private readonly Random _random = new();
        private bool IsRandomReply(Channel channel)
            => channel.RandomReplyChance != 0 && channel.RandomReplyChance > (_random.Next(99) + 0.001 + _random.NextDouble());
        
        private bool UserIsHunted(Channel channel, ulong userId)
            => channel.Guild.HuntedUsers.FirstOrDefault(u => u.UserId == userId && u.Chance > (_random.Next(99) + 0.001 + _random.NextDouble())) is not null;

        private async Task TryToCallCharacterAsync(SocketCommandContext context, Channel channel, StorageContext db)
        {
            string? historyId = channel.HistoryId ?? await CreateNewChatHistoryForChannel(channel, db);

            if (historyId is null)
            {
                await context.Message.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} Failed to find or create a new chat with the character".ToInlineEmbed(Color.Red));
                return;
            }

            // Reformat message
            var formatTemplate = channel.ChannelMessagesFormat ?? channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            string username = context.User is SocketUser user ? user.GetBestName() : context.User.Username;
            
            string text = context.Message.Content ?? "";
            text = TryToRemoveTextPrefix(text);
            text = formatTemplate.Replace("{{user}}", username).Replace("{{msg}}", text).Replace("\\n", "\n")
                                 .AddRefQuote(context.Message.ReferencedMessage);

            // Get character response
            var characterResponse = await _integration.CallCaiCharacterAsync(text, historyId, channel.LastCharacterMsgId, null);
            if (characterResponse.IsFailure)
            {
                await context.Message.ReplyAsync(embed: characterResponse.Text.ToInlineEmbed(Color.Red));
                return;
            }

            var messageId = await TryToSendCharacterMessageAsync(historyId, characterResponse, context);
            if (messageId is not null)
            {
                TryToAddButtons(channel, context, (ulong)messageId);
                TryToRemoveButtons(channel.LastCharacterDiscordMsgId, context.Channel);
            }

            channel.CurrentSwipeIndex = 0;
            channel.LastCharacterMsgId = characterResponse.CharacterMessageId;
            channel.LastUserMsgId = characterResponse.UserMessageId;
            channel.LastDiscordUserCallerId = context.User.Id;
            channel.LastCallTime = DateTime.UtcNow;
            channel.MessagesSent++;
            channel.LastCharacterDiscordMsgId = messageId ?? 0;
            
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException e)
            {
                e.Entries.Single().Reload();
                db.SaveChanges();
            }
        }

        private string TryToRemoveTextPrefix(string text)
        {
            if (text.StartsWith("<"))
            {
                text = MentionRegex().Replace(text, "", 1);
            }
            else
            {
                foreach (string prefix in _discordService.Prefixes)
                {
                    if (!text.StartsWith(prefix)) continue;

                    text = text.RemovePrefix(prefix);
                    break;
                }
            }

            return text;
        }

        private async Task<string?> CreateNewChatHistoryForChannel(Channel channel, StorageContext db)
        {
            string? historyId = await _integration.CaiClient.CreateNewChatAsync(_integration.SelfCharacter!.Id, _integration.CaiAuthToken, _integration.CaiPlusMode);
            if (historyId is null) return null;

            db.Entry(channel).Reload();
            channel.HistoryId = historyId;
            await db.SaveChangesAsync();

            return historyId;
        }

        /// <returns>Message ID</returns>
        private async Task<ulong?> TryToSendCharacterMessageAsync(string historyId, CharacterResponse characterResponse, SocketCommandContext context)
        {
            var availResponses = _integration.AvailableCharacterResponses;
            availResponses.TryAdd(historyId, new());

            lock (availResponses[historyId])
            {
                // Forget all choises from the last message and remember a new one
                availResponses[historyId].Clear();
                availResponses[historyId].Add(new()
                {
                    Text = characterResponse.Text,
                    MessageId = characterResponse.CharacterMessageId,
                    ImageUrl = characterResponse.ImageRelPath
                });
            }

            string characterMessage = characterResponse.Text;

            // Cut if too long
            if (characterMessage.Length > 2000)
                characterMessage = characterMessage[0..1994] + "[...]";

            // Fill embeds
            Embed? embed = null;
            if (characterResponse.ImageRelPath is not null)
            {
                bool canGetImage = await TryGetImageAsync(characterResponse.ImageRelPath, _integration.HttpClient);
                if (canGetImage)
                    embed = new EmbedBuilder().WithImageUrl(characterResponse.ImageRelPath).Build();
            }

            // Sending message
            var message = await context.Message.ReplyAsync(characterMessage, embed: embed);

            return message?.Id;
        }

        private static void TryToAddButtons(Channel channel, SocketCommandContext context, ulong messageId)
        {
            _ = Task.Run(async () =>
            {
                bool callerIsBot = context.User.IsWebhook || context.User.IsBot;

                IMessage message;
                try
                {
                    message = await context.Channel.GetMessageAsync(messageId);
                }
                catch
                {
                    return;
                }

                try
                {
                    if (channel.SwipesEnabled && !callerIsBot)
                    {
                        await message.AddReactionAsync(ARROW_LEFT);
                        await message.AddReactionAsync(ARROW_RIGHT);
                    }

                    if (channel.StopBtnEnabled && callerIsBot)
                    {
                        await message.AddReactionAsync(STOP_BTN);
                    }
                }
                catch
                {
                    await context.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to add reaction-buttons to the character message.\nMake sure that bot has permission to add reactions in this channel.".ToInlineEmbed(Color.Red));
                }
            });
        }

        private void TryToRemoveButtons(ulong oldMessageId, ISocketMessageChannel channel)
        {
            _ = Task.Run(async () =>
            {
                var oldMessage = await channel.GetMessageAsync(oldMessageId);
                if (oldMessage is null) return;

                var btns = new Emoji[] { ARROW_LEFT, ARROW_RIGHT };
                await Parallel.ForEachAsync(btns, async (btn, ct)
                    => await oldMessage.RemoveReactionAsync(btn, _client.CurrentUser));
            });
        }

        private void HandleTextMessageException(SocketMessage message, Exception e)
        {
            LogException(new[] { e });

            if (e.Message.Contains("Missing Permissions")) return;

            var channel = message.Channel as SocketGuildChannel;
            var guild = channel?.Guild;
            TryToReportInLogsChannel(_client, title: "Message Exception",
                                              desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                    $"Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                    $"User: {message.Author.Username}" + (message.Author.IsWebhook ? " (webhook)" : message.Author.IsBot ? " (bot)" : "") +
                                                    $"\nMessage: {message.Content[0..Math.Min(message.Content.Length, 1000)]}",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }
    }
}
