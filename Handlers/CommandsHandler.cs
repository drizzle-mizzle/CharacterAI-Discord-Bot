using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using CharacterAI_Discord_Bot.Service;
using CharacterAI_Discord_Bot.Models;
using CharacterAI;
using System.Threading.Channels;

namespace CharacterAI_Discord_Bot.Handlers
{
    public class CommandsHandler : HandlerService
    {
        internal Integration CurrentIntegration { get; }
        internal int ReplyChance { get; set; } // 0
        internal List<ulong> BlackList { get; set; } = new();
        internal List<Models.Channel> Channels { get; set; } = new();
        internal LastSearchQuery? LastSearch { get; set; }
        internal Dictionary<ulong, int> HuntedUsers { get; set; } = new(); // user id : reply chance

        private readonly Dictionary<ulong, int[]> _userMsgCount = new();
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;

        public CommandsHandler(IServiceProvider services)
        {
            CurrentIntegration = new(BotConfig.UserToken);

            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();

            _client.MessageReceived += HandleMessage;
            _client.ReactionAdded += HandleReaction;
            _client.ReactionRemoved += HandleReaction;
            _client.ButtonExecuted += HandleButton;
        }

        private async Task HandleMessage(SocketMessage rawMsg)
        {
            var authorId = rawMsg.Author.Id;
            if (rawMsg is not SocketUserMessage message || authorId == _client.CurrentUser.Id)
                return;

            var context = new SocketCommandContext(_client, message);

            int argPos = 0;
            var randomNumber = new Random();
            string[] prefixes = BotConfig.BotPrefixes;

            bool isDM = context.Guild is null;
            bool hasMention = isDM || message.HasMentionPrefix(_client.CurrentUser, ref argPos);
            bool hasPrefix = hasMention || prefixes.Any(p => message.HasStringPrefix(p, ref argPos));
            bool hasReply = hasPrefix || (message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id); // IT'S SO FUCKING BIG UUUGHH!
            bool randomReply = hasReply || (ReplyChance >= randomNumber.Next(100) + 1);
            // Any condition above or if user is hunted
            bool gottaReply = randomReply || (HuntedUsers.ContainsKey(authorId) && HuntedUsers[authorId] >= randomNumber.Next(100) + 1);

            if (!gottaReply) return;
            
            // Update messages-per-minute counter.
            // If user has exceeded rate limit, or if message is a DM and these are disabled - return
            if ((isDM && !BotConfig.DMenabled) || UserIsBanned(context)) return;

            // Try to execute command
            var cmdResponse = await _commands.ExecuteAsync(context, argPos, _services);
            // If command was found and executed, return
            if (cmdResponse.IsSuccess) return;
            // If command was found but failed to execute, return
            if (cmdResponse.ErrorReason != "Unknown command.")
            {
                string text = $"⚠ Failed to execute command: {cmdResponse.ErrorReason} ({cmdResponse.Error})";
                if (isDM) text = "*Note: some commands are not intended to be called from DMs*\n" + text;

                await message.ReplyAsync(text).ConfigureAwait(false);
                return;
            }

            // If command was not found, perform character call
            var cI = CurrentIntegration; // shortcut for readability
            if (cI.CurrentCharacter.IsEmpty)
            {
                await context.Message.ReplyAsync("⚠ Set a character first").ConfigureAwait(false);
                return;
            }

            var currentChannel = Channels.Find(c => c.Id == context.Channel.Id);
            bool isPrivate = context.Channel.Name.StartsWith("private");

            if (currentChannel is null)
            {
                if (isPrivate) return;

                string? historyId = null;
                if (isDM) historyId = await cI.CreateNewChatAsync();
                historyId ??= cI.Chats[0];

                currentChannel = new Models.Channel(context.Channel.Id, context.User.Id, historyId, cI.CurrentCharacter.Id!);

                Channels.Add(currentChannel);
                SaveData(channels: Channels);
            }

            if (currentChannel.Data.SkipMessages > 0)
                currentChannel.Data.SkipMessages--;
            else
                using (message.Channel.EnterTypingState())
                    _ = TryToCallCharacterAsync(context, currentChannel, isDM || isPrivate);
        }

        private async Task HandleReaction(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var message = await rawMessage.DownloadAsync();
            var currentChannel = Channels.Find(c => c.Id == message.Channel.Id);
            if (currentChannel is null) return;

            if (currentChannel.Data.LastCall is null || rawMessage.Id != currentChannel.Data.LastCharacterCallMsgId)
                return;

            var user = reaction.User.Value as SocketUser;
            if (user!.IsBot || user.Id != message.ReferencedMessage.Author.Id)
                return;

            if (reaction.Emote.Name == new Emoji("\u2B05").Name && currentChannel.Data.LastCall!.CurrentReplyIndex > 0)
            {   // left arrow
                currentChannel.Data.LastCall!.CurrentReplyIndex--;
                _ = UpdateMessageAsync(message, currentChannel);

                return;
            }
            if (reaction.Emote.Name == new Emoji("\u27A1").Name)
            {   // right arrow
                currentChannel.Data.LastCall!.CurrentReplyIndex++;
                _ = UpdateMessageAsync(message, currentChannel);

                return;
            }
        }

        // Navigate in search modal
        private async Task HandleButton(SocketMessageComponent component)
        {
            if (LastSearch is null) return;

            var context = new SocketCommandContext(_client, component.Message);
            var refMessage = await context.Message.Channel.GetMessageAsync(context.Message.Reference!.MessageId.Value);
            bool notAuthor = component.User.Id != refMessage.Author.Id;
            bool noPages = LastSearch!.Response.IsEmpty;
            if (notAuthor || UserIsBanned(context) || noPages) return;

            int tail = LastSearch!.Response!.Characters!.Count - (LastSearch.CurrentPage - 1) * 10;
            int maxRow = tail > 10 ? 10 : tail;

            switch (component.Data.CustomId)
            { // looks like shit...
                case "up":
                    if (LastSearch.CurrentRow == 1)
                        LastSearch.CurrentRow = maxRow;
                    else
                        LastSearch.CurrentRow--;
                    break;
                case "down":
                    if (LastSearch.CurrentRow > maxRow)
                        LastSearch.CurrentRow = 1;
                    else
                        LastSearch.CurrentRow++;
                    break;
                case "left":
                    LastSearch.CurrentRow = 1;

                    if (LastSearch.CurrentPage == 1)
                        LastSearch.CurrentPage = LastSearch.Pages;
                    else
                        LastSearch.CurrentPage--;
                    break;
                case "right":
                    LastSearch.CurrentRow = 1;

                    if (LastSearch.CurrentPage == LastSearch.Pages)
                        LastSearch.CurrentPage = 1;
                    else
                        LastSearch.CurrentPage++;
                    break;
                case "select":
                    var refContext = new SocketCommandContext(_client, (SocketUserMessage)refMessage);

                    using (refContext.Message.Channel.EnterTypingState())
                    {
                        int index = (LastSearch.CurrentPage - 1) * 10 + LastSearch.CurrentRow - 1;
                        var character = LastSearch.Response!.Characters![index];
                        if (character.IsEmpty) return;

                        _ = CommandsService.SetCharacterAsync(character.Id!, this, refContext);
                        await component.UpdateAsync(c =>
                        {
                            var imageUrl = TryGetImage(character.AvatarUrlFull!).Result ?
                                            character.AvatarUrlFull : TryGetImage(character.AvatarUrlMini!).Result ?
                                                character.AvatarUrlMini : null;

                            string desc = $"{character.Description}\n\n" +
                                          $"*Original link: [Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})*";
                            c.Embed = new EmbedBuilder()
                            {
                                Title = $"✅ Selected - {character.Name}",
                                Description = desc,
                                ImageUrl = imageUrl,
                                Footer = new EmbedFooterBuilder().WithText($"Created by {character.Author}")
                            }.Build();
                            c.Components = null;
                        }).ConfigureAwait(false);
                    }
                    return;
                default:
                    return;
            }

            // Only if left/right/up/down is selected
            await component.UpdateAsync(c => c.Embed = BuildCharactersList(LastSearch))
                           .ConfigureAwait(false);
        }

        // Swipes
        private async Task UpdateMessageAsync(IUserMessage message, Models.Channel currentChannel)
        {
            if (currentChannel.Data.LastCall!.RepliesList.Count < currentChannel.Data.LastCall.CurrentReplyIndex + 1)
            {
                _ = message.ModifyAsync(msg => { msg.Content = $"( 🕓 Wait... )"; msg.AllowedMentions = AllowedMentions.None; });
                var historyId = currentChannel.Data.HistoryId;
                var parentMsgId = currentChannel.Data.LastCall.OriginalResponse.LastUserMsgId;
                var response = await CurrentIntegration.CallCharacterAsync(parentMsgId: parentMsgId, historyId: historyId);

                if (!response.IsSuccessful)
                {
                    _ = message.ModifyAsync(msg => { msg.Content = $"⚠ Somethinh went wrong!"; });
                    return;
                }
                currentChannel.Data.LastCall.RepliesList.AddRange(response.Replies);
            }
            var newReply = currentChannel.Data.LastCall.RepliesList[currentChannel.Data.LastCall.CurrentReplyIndex];
            currentChannel.Data.LastCall.CurrentPrimaryMsgId = newReply.Id;

            Embed? embed = null;
            if (newReply.HasImage && await TryGetImage(newReply.ImageRelPath!))
                embed = new EmbedBuilder().WithImageUrl(newReply.ImageRelPath).Build();

            _ = message.ModifyAsync(msg => { msg.Content = $"{newReply.Text}"; msg.Embed = embed; })
                .ConfigureAwait(false);
        }

        private async Task TryToCallCharacterAsync(SocketCommandContext context, Models.Channel currentChannel, bool isPrivate)
        {
            // Get last call and remove buttons from it
            if (currentChannel.Data.LastCharacterCallMsgId != 0)
            {
                var lastMessage = await context.Message.Channel.GetMessageAsync(currentChannel.Data.LastCharacterCallMsgId);
                _ = RemoveButtons(lastMessage);
            }

            string text = RemoveMention(context.Message.Content);
            string? imgPath = null;

            // Prepare call data
            int amode = currentChannel.Data.AudienceMode;
            if (amode == 1 || amode == 3)
                text = AddUsername(text, context.Message);
            if (amode == 2 || amode == 3)
                text = AddQuote(text, context.Message);

            if (context.Message.Attachments.Any())
            {   // Downloads first image from attachments and uploads it to server
                string url = context.Message.Attachments.First().Url;
                if (await TryDownloadImg(url, 10) is byte[] @img && await CurrentIntegration.UploadImageAsync(@img) is string @path)
                    imgPath = $"https://characterai.io/i/400/static/user/{@path}";
            }

            string historyId = currentChannel.Data.HistoryId;
            ulong? primaryMsgId = currentChannel.Data.LastCall?.CurrentPrimaryMsgId;

            // Send message to the character
            var response = await CurrentIntegration.CallCharacterAsync(text, imgPath, historyId, primaryMsgId);
            currentChannel.Data.LastCall = new(response);

            // Alert with error message if call fails
            if (!response.IsSuccessful)
            {
                await context.Message.ReplyAsync(response.ErrorReason).ConfigureAwait(false);
                return;
            }

            // Take first character answer by default and reply with it
            var reply = currentChannel.Data.LastCall!.RepliesList.First();
            _ = Task.Run(async () => currentChannel.Data.LastCharacterCallMsgId = await ReplyOnMessage(context.Message, reply, isPrivate));
        }

        private bool UserIsBanned(SocketCommandContext context)
        {
            ulong currUserId = context.Message.Author.Id;
            if (context.Guild is not null && currUserId == context.Guild.OwnerId)
                return false;

            if (BlackList.Contains(currUserId)) return true;

            int currMinute = context.Message.CreatedAt.Minute + context.Message.CreatedAt.Hour * 60;

            // Start watching for user
            if (!_userMsgCount.ContainsKey(currUserId))
                _userMsgCount.Add(currUserId, new int[] { -1, 0 }); // current minute : count

            // Drop + update user stats if he replies in new minute
            if (_userMsgCount[currUserId][0] != currMinute)
            {
                _userMsgCount[currUserId][0] = currMinute;
                _userMsgCount[currUserId][1] = 0;
            }

            // Update messages count withing current minute
            _userMsgCount[currUserId][1]++;

            if (_userMsgCount[currUserId][1] == BotConfig.RateLimit - 1)
                context.Message.ReplyAsync($"⚠ Warning! If you proceed to call {context.Client.CurrentUser.Mention} " +
                                            "so fast, you'll be blocked from using it.");
            else if (_userMsgCount[currUserId][1] > BotConfig.RateLimit)
            {
                BlackList.Add(currUserId);
                _userMsgCount.Remove(currUserId);

                return true;
            }

            return false;
        }

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}