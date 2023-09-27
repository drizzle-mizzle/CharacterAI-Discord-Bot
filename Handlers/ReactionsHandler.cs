using CharacterAI;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using CharacterAiDiscordBot.Services;
using CharacterAiDiscordBot.Models.Database;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterAiDiscordBot.Models.Common;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.EntityFrameworkCore;

namespace CharacterAiDiscordBot.Handlers
{
    internal class ReactionsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationService _integration;

        public ReactionsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.ReactionAdded += (msg, channel, reaction) =>
            {
                Task.Run(async () =>
                {
                    try { await HandleReactionAsync(msg, channel, reaction); }
                    catch (Exception e) { await HandleReactionException(channel, reaction, e); }
                });
                return Task.CompletedTask;
            };

            _client.ReactionRemoved += (msg, channel, reaction) =>
            {
                Task.Run(async () =>
                {
                    try { await HandleReactionAsync(msg, channel, reaction); }
                    catch (Exception e) { await HandleReactionException(channel, reaction, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> discordChannel, SocketReaction reaction)
        {
            var user = reaction.User.GetValueOrDefault();
            if (user is null || user is not SocketUser userReacted || userReacted.IsBot) return;

            IUserMessage originalMessage;
            try { originalMessage = await rawMessage.DownloadAsync(); }
            catch { return; }

            if (originalMessage.Author.Id != _client.CurrentUser.Id) return;

            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(discordChannel.Id);
            if (channel is null) return;

            if ((reaction.Emote?.Name == STOP_BTN.Name) && channel.StopBtnEnabled)
            {
                channel.SkipNextBotMessage = true;
                await db.SaveChangesAsync();
                return;
            }

            //if (reaction.Emote.Name == TRANSLATE_BTN.Name)
            //{
            //    _ = TranslateMessageAsync(message, currentChannel.Data.TranslateLanguage);
            //    return;
            //}

            bool userIsLastCaller = channel.LastDiscordUserCallerId == userReacted.Id;
            bool msgIsSwipable = originalMessage.Id == channel.LastCharacterDiscordMsgId;
            if (!(userIsLastCaller && msgIsSwipable)) return;

            if ((reaction.Emote?.Name == ARROW_LEFT.Name) && channel.CurrentSwipeIndex > 0)
            {   // left arrow
                if (await _integration.CheckIfUserIsBannedAsync(reaction, _client)) return;

                channel.CurrentSwipeIndex--;
                await db.SaveChangesAsync();
                await UpdateCharacterMessage(originalMessage, channel);
            }
            else if (reaction.Emote?.Name == ARROW_RIGHT.Name)
            {   // right arrow
                if (await _integration.CheckIfUserIsBannedAsync(reaction, _client)) return;

                channel.CurrentSwipeIndex++;
                await db.SaveChangesAsync();
                await UpdateCharacterMessage(originalMessage, channel);
            }
        }

        /// <summary>
        /// Super complicated shit, but I don't want to refactor it...
        /// </summary>
        private async Task UpdateCharacterMessage(IUserMessage characterOriginalMessage, Channel channel)
        {
            var availResponses = _integration.AvailableCharacterResponses;
            if (channel.HistoryId is null || !availResponses.ContainsKey(channel.HistoryId)) return;

            var db = new StorageContext();

            // Check if fetching a new message, or just swiping among already available ones
            bool gottaFetch = availResponses[channel.HistoryId].Count < channel.CurrentSwipeIndex + 1;
            if (gottaFetch)
            {
                await characterOriginalMessage.ModifyAsync(msg =>
                {
                    msg.Content = null;
                    msg.Embed = WAIT_MESSAGE;
                    msg.AllowedMentions = AllowedMentions.None;
                });

                var characterResponse = await _integration.CallCaiCharacterAsync("", channel.HistoryId, null, channel.LastUserMsgId);

                if (characterResponse.IsFailure)
                {
                    await characterOriginalMessage.ModifyAsync(msg =>
                    {
                        msg.Embed = characterResponse.Text.ToInlineEmbed(Color.Red);
                        msg.AllowedMentions = AllowedMentions.All;
                    });
                    return;
                }

                // Add to the storage
                var newResponse = new AvailableCharacterResponse()
                {
                    MessageId = characterResponse.CharacterMessageId!,
                    Text = characterResponse.Text,
                    ImageUrl = characterResponse.ImageRelPath
                };

                lock (availResponses)
                {
                    availResponses[channel.HistoryId].Add(newResponse);
                }
            }

            AvailableCharacterResponse newCharacterMessage;
            try { newCharacterMessage = availResponses[channel.HistoryId][channel.CurrentSwipeIndex]; }
            catch { return; }

            channel.LastCharacterMsgId = newCharacterMessage.MessageId;

            // Add image the message
            Embed? embed = null;
            string? imageUrl = newCharacterMessage.ImageUrl;

            if (imageUrl is not null && await TryGetImageAsync(imageUrl, _integration.HttpClient))
                embed = new EmbedBuilder().WithImageUrl(imageUrl).Build();

            // Add text to the message
            string responseText = newCharacterMessage.Text ?? " ";
            if (responseText.Length > 2000)
                responseText = responseText[0..1994] + "[...]";

            // Send (update) message
            if (responseText.Length > 2000) responseText = responseText[0..1994] + "[max]";

            try
            {
                await characterOriginalMessage.ModifyAsync(msg =>
                {
                    msg.Content = responseText;
                    msg.Embed = embed;
                    msg.AllowedMentions = AllowedMentions.All;
                });
            }
            catch
            {
                return;
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException e)
            {
                e.Entries.Single().Reload();
                db.SaveChanges();
            }
            //var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            //if (tm is not null) tm.IsTranslated = false;
        }

        private async Task HandleReactionException(Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, Exception e)
        {
            LogException(new[] { e });
            var guildChannel = (await channel.GetOrDownloadAsync()) as SocketGuildChannel;
            var guild = guildChannel?.Guild;
            TryToReportInLogsChannel(_client, title: "Reaction Exception",
                                              desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                    $"Channel: `{guildChannel?.Name} ({guildChannel?.Id})`\n" +
                                                    $"User: `{reaction.User.GetValueOrDefault()?.Username}`\n" +
                                                    $"Reaction: {reaction.Emote.Name}",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }
    }
}
   