using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using CharacterAI_Discord_Bot.Service;

namespace CharacterAI_Discord_Bot.Handlers
{
    public class CommandsHandler : HandlerService
    {
        private readonly Dictionary<ulong, int[]> _userMsgCount = new();
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;

        public CommandsHandler(IServiceProvider services)
        {
            CreateIntegration(BotConfig.caiUserToken);

            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();

            _client.MessageReceived += HandleMessage;
            _client.ButtonExecuted += HandleButtonAsync;
            _client.ReactionAdded += HandleReactionAsync;
            _client.ReactionRemoved += HandleReactionAsync;

            _client.JoinedGuild += (s) => Task.Run(() =>
            {
                if (CurrentIntegration.CurrentCharacter.Name is string name)
                    _ = SetBotNicknameAndRole(name, _client);
            });

            if (BotConfig.TranslateBtnEnabled && !string.IsNullOrWhiteSpace(BotConfig.DeeplApiToken))
                DeeplClient = new(BotConfig.DeeplApiToken);
        }

        private void CreateIntegration(string token)
            => CurrentIntegration = new(token, BotConfig.CAIplusEnabled);

        private Task HandleMessage(SocketMessage rawMsg)
        {
            _ = Task.Run(async () => await HandleMessageAsync(rawMsg));
            return Task.CompletedTask;
        }

        private async Task HandleMessageAsync(SocketMessage rawMsg)
        {
            var authorId = rawMsg.Author.Id;
            if (rawMsg is not SocketUserMessage message || authorId == _client.CurrentUser.Id)
                return;

            int argPos = 0;
            string[] prefixes = BotConfig.BotCallPrefixes;

            var cI = CurrentIntegration;
            var context = new SocketCommandContext(_client, message);
            var currentChannel = Channels.Find(c => c.ChannelId == context.Channel.Id);

            bool isPrivate = context.Channel.Name.StartsWith("private");
            bool isDM = context.Guild is null;
            bool hasMention = message.HasMentionPrefix(_client.CurrentUser, ref argPos);
            bool hasPrefix = hasMention || prefixes.Any(p => message.HasStringPrefix(p, ref argPos));
            bool hasReply = hasPrefix || message.ReferencedMessage is IUserMessage refm && refm.Author.Id == _client.CurrentUser.Id;

            bool cnn = currentChannel is not null;
            bool randomReply = cnn && currentChannel!.Data.ReplyChance > (@Random.Next(99) + 0.001 + @Random.NextDouble()); // min: 0 + 0.001 + 0 = 0.001; max: 98 + 0.001 + 1 = 99.001
            bool userIsHunted = cnn && currentChannel!.Data.HuntedUsers.ContainsKey(authorId) && currentChannel.Data.HuntedUsers[authorId] >= @Random.Next(100) + 1;
            bool gottaExecute = hasMention || hasPrefix;
            bool gottaReply = isDM || hasReply || randomReply || userIsHunted;
            if (!(gottaExecute || gottaReply)) return;

            // Don't handle deactivated "private" chats
            if (currentChannel is null && isPrivate) return;
            
            // If new channel, add it to the local channels database
            currentChannel ??= await StartTrackingChannelAsync(context);                

            // Update messages-per-minute counter.
            // If user has exceeded rate limit, or if message is a DM and these are disabled - return
            if ((isDM && !BotConfig.DMenabled) || UserIsBanned(context)) return;

            if (gottaExecute)
            {
                var cmdArgPos = message.Content.IndexOf(BotConfig.BotCommandSubPrefix) + 1; 
                // Try to execute command
                var cmdResponse = await _commands.ExecuteAsync(context, cmdArgPos, _services);
                // If command was found and executed, return
                if (cmdResponse.IsSuccess) return;
                // If command was found but failed to execute, return
                if (cmdResponse.ErrorReason != "Unknown command.")
                {
                    string text = $"{WARN_SIGN_DISCORD} Failed to execute command: {cmdResponse.ErrorReason} ({cmdResponse.Error})";
                    if (isDM) text = "*Note: some commands are not intended to be called from DMs*\n" + text;

                    await message.ReplyAsync(text).ConfigureAwait(false);
                    return;
                } // If command was not found, proceed with character call
            }

            if (cI.CurrentCharacter.IsEmpty)
            {
                await message.ReplyAsync($"{WARN_SIGN_DISCORD} Set a character first").ConfigureAwait(false);
                return;
            }
            // "Stop button"
            if (message.Author.IsBot && currentChannel.Data.SkipNextBotMessage)
            {
                currentChannel.Data.SkipNextBotMessage = false;
                return;
            }
            // "skip message" command
            if (currentChannel.Data.SkipMessages > 0)
                currentChannel.Data.SkipMessages--;
            else
            {
                var typing = context.Channel.EnterTypingState();
                try { _ = TryToCallCharacterAsync(context, currentChannel, isDM || isPrivate); }
                catch (Exception e) { Failure(e.ToString(), client: _client); }

                await Task.Delay(2500);
                typing.Dispose();
            }       
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var user = reaction?.User.Value;
            if (user is null) return;

            var socketUser = (SocketUser)user;
            if (socketUser.IsBot) return;

            var message = await rawMessage.DownloadAsync();
            var currentChannel = Channels.Find(c => c.ChannelId == message.Channel.Id);
            if (currentChannel is null) return;
            if (reaction is null) return;

            if (reaction.Emote.Name == STOP_BTN.Name)
            {
                currentChannel.Data.SkipNextBotMessage = true;
                return;
            }

            if (reaction.Emote.Name == TRANSLATE_BTN.Name)
            {
                _ = TranslateMessageAsync(message, currentChannel.Data.TranslateLanguage);
                return;
            }

            bool userIsLastMessageAuthor = message.ReferencedMessage is IUserMessage um && socketUser.Id == um.Author.Id;
            bool msgIsSwipable = currentChannel.Data.LastCall is not null && rawMessage.Id == currentChannel.Data.LastCharacterCallMsgId;
            if (!userIsLastMessageAuthor || !msgIsSwipable) return;

            if (reaction.Emote.Name == ARROW_LEFT.Name && currentChannel.Data.LastCall!.CurrentReplyIndex > 0)
            {   // left arrow
                currentChannel.Data.LastCall!.CurrentReplyIndex--;
                _ = SwipeMessageAsync(message, currentChannel);

                return;
            }
            if (reaction.Emote.Name == ARROW_RIGHT.Name)
            {   // right arrow
                currentChannel.Data.LastCall!.CurrentReplyIndex++;
                _ = SwipeMessageAsync(message, currentChannel);

                return;
            }
        }

        // Navigate in search modal
        private async Task HandleButtonAsync(SocketMessageComponent component)
        {
            if (LastSearch is null) return;

            var context = new SocketCommandContext(_client, component.Message);
            var refMessage = await context.Message.Channel.GetMessageAsync(context.Message.Reference!.MessageId.Value);
            bool notAuthor = component.User.Id != refMessage.Author.Id;
            bool noPages = LastSearch!.Response.IsEmpty;
            if (notAuthor || UserIsBanned(context, checkOnly: true) || noPages) return;

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
                    await component.DeferAsync();
                    var refContext = new SocketCommandContext(context.Client, (SocketUserMessage)refMessage);
                    _ = SelectCharacterAsync(this, component, refContext);

                    return;
                default:
                    return;
            }

            // Only if left/right/up/down is selected, either this line will never be reached
            await component.UpdateAsync(c => c.Embed = BuildCharactersList(LastSearch)).ConfigureAwait(false);
        }

        // Swipes
        private async Task SwipeMessageAsync(IUserMessage message, Models.DiscordChannel currentChannel)
        {
            var lastCall = currentChannel.Data.LastCall!;

            // Drop delay
            if (RemoveEmojiRequestQueue.ContainsKey(message.Id))
                RemoveEmojiRequestQueue[message.Id] = BotConfig.BtnsRemoveDelay;

            // Check if fetching a new message, or just swiping among already available ones
            if (lastCall.RepliesList.Count < lastCall.CurrentReplyIndex + 1)
            {
                _ = message.ModifyAsync(msg => { msg.Content = WAIT_MESSAGE; msg.AllowedMentions = AllowedMentions.None; }).ConfigureAwait(false);

                // Get new character response
                var historyId = currentChannel.Data.HistoryId;
                var parentMsgId = lastCall.OriginalResponse.LastUserMsgId;
                var response = await CurrentIntegration.CallCharacterAsync(parentMsgId: parentMsgId, historyId: historyId);
                if (!response.IsSuccessful)
                {
                    await message.ModifyAsync(msg => { msg.Content = response.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD); }).ConfigureAwait(false); ;
                    return;
                }
                lastCall.RepliesList.Add(response.Response!);
            }
            var newReply = lastCall.RepliesList[lastCall.CurrentReplyIndex];
            lastCall.CurrentPrimaryMsgId = newReply.Id;

            // Add image to the message
            Embed? embed = null;
            if (newReply.HasImage && await TryGetImageAsync(newReply.ImageRelPath!, @HttpClient))
                embed = new EmbedBuilder().WithImageUrl(newReply.ImageRelPath).Build();

            // Add text to the message
            string replyText = newReply.Text!;
            if (replyText.Length > 2000)
                replyText = replyText[0..1994] + "[...]";

            // Send (update) message
            await message.ModifyAsync(msg => { msg.Content = $"{replyText}"; msg.Embed = embed; }).ConfigureAwait(false);
            var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            if (tm is not null) tm.IsTranslated = false;
        }

        private async Task TryToCallCharacterAsync(SocketCommandContext context, Models.DiscordChannel currentChannel, bool inDMorPrivate)
        {
            // Get last call and remove buttons from it
            if (currentChannel.Data.LastCharacterCallMsgId != 0)
            {
                var lastMessage = await context.Message.Channel.GetMessageAsync(currentChannel.Data.LastCharacterCallMsgId);
                _ = RemoveButtonsAsync(lastMessage).ConfigureAwait(false);
            }

            // Prepare text data
            string text = RemoveMentionAndPrefix(context.Message.Content);

            int amode = currentChannel.Data.AudienceMode;
            if (amode == 1 || amode == 3 || inDMorPrivate)
                text = AddUsername(text, context);
            if (amode == 2 || amode == 3)
                text = AddQuote(text, context.Message);

            // Prepare image data
            string? imgPath = null;
            //var attachments = context.Message.Attachments;
            //if (attachments.Any())
            //{   // Downloads first image from attachments and uploads it to server
            //    var file = attachments.First();
            //    string url = file.Url;
            //    string fileName = file.Filename; 
            //    var image = await TryDownloadImgAsync(url);

            //    bool isDownloaded = image is not null;
            //    string? path = isDownloaded ? await CurrentIntegration.UploadImageAsync(image!, fileName) : null;

            //    if (path is not null)
            //        imgPath = $"https://characterai.io/i/400/static/user/{path}";
            //}
            
            string historyId = currentChannel.Data.HistoryId!;
            ulong? primaryMsgId = currentChannel.Data.LastCall?.CurrentPrimaryMsgId;

            // Send message to a character
            await Task.Delay(currentChannel.Data.ReplyDelay * 1000); // wait
            var response = await CurrentIntegration.CallCharacterAsync(text, imgPath, historyId, primaryMsgId);

            if (response.IsSuccessful)
            {
                currentChannel.Data.LastCall = new(response);
                // Take first character answer by default and respond with it
                var reply = currentChannel.Data.LastCall!.RepliesList.First();
                _ = Task.Run(async () =>
                {
                    var msgId = await RespondOnMessage(context.Message, reply, inDMorPrivate);
                    currentChannel.Data.LastCharacterCallMsgId = msgId;
                });
            }
            else // Alert with error message if call fails
                await context.Message.ReplyAsync(response.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD)).ConfigureAwait(false);
        }

        private bool UserIsBanned(SocketCommandContext context, bool checkOnly = false)
        {
            ulong currUserId = context.Message.Author.Id;
            if (context.Guild is not null && !BotConfig.PublicMode && currUserId == context.Guild.OwnerId)
                return false;

            if (BlackList.Contains(currUserId)) return true;
            if (checkOnly) return false;

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
                context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} Warning! If you proceed to call {context.Client.CurrentUser.Mention} " +
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
