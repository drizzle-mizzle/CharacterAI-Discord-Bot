using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Dynamic;
using CharacterAI_Discord_Bot.Service;
using CharacterAI_Discord_Bot.Models;

namespace CharacterAI_Discord_Bot.Handlers
{
    public class CommandsHandler : HandlerService
    {
        public LastSearch? lastSearch;
        public HandlerTemps temps = new();
        public LastResponse lastResponse = new();
        public readonly Integration integration = new(Config.userToken);
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;

        public CommandsHandler(IServiceProvider services)
        {
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
            if (rawMsg is not SocketUserMessage message || message.Author.Id == _client.CurrentUser.Id)
                return;

            int argPos = 0;
            string[] prefixes = Config.botPrefixes;
            var RandomGen = new Random();

            bool hasMention = message.HasMentionPrefix(_client.CurrentUser, ref argPos);
            bool hasPrefix = !hasMention && prefixes.Any(p => message.HasStringPrefix(p, ref argPos));
            bool hasReply = !hasPrefix && !hasMention && message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id; // SO FUCKING BIG UUUGHH!
            bool randomReply = temps.replyChance >= RandomGen.Next(100) + 1;
            bool userIsHunted = temps.huntedUsers.Contains(message.Author.Id) && temps.huntChance >= RandomGen.Next(100) + 1;

            if (hasMention || hasPrefix || hasReply || userIsHunted || randomReply)
            {
                if (UserIsBanned(message).Result) return;

                var context = new SocketCommandContext(_client, message);
                var cmdResponse = await _commands.ExecuteAsync(context, argPos, _services);

                if (!cmdResponse.IsSuccess)
                {
                    if (cmdResponse.ErrorReason != "Unknown command.")
                        await message.ReplyAsync($"⚠ {cmdResponse.ErrorReason}, {cmdResponse.Error}").ConfigureAwait(false);
                    else if (temps.skipMessages > 0)
                        temps.skipMessages--;
                    else
                        using (message.Channel.EnterTypingState())
                            _ = CallCharacterAsync(message);
                }
            }
        }

        private async Task HandleReaction(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (lastResponse.replies is null || rawMessage.Id != temps.lastCharacterCallMsgId)
                return;

            var message = await rawMessage.DownloadAsync();
            var user = reaction.User.Value as SocketGuildUser;
            if (user!.IsBot || user.Id != message.ReferencedMessage.Author.Id)
                return;

            if (reaction.Emote.Name == new Emoji("\u2B05").Name && lastResponse.currReply > 0)
            {   // left arrow
                lastResponse.currReply--;
                _ = UpdateMessageAsync(message);

                return;
            }
            if (reaction.Emote.Name == new Emoji("\u27A1").Name)
            {   // right arrow
                lastResponse.currReply++;
                _ = UpdateMessageAsync(message);

                return;
            }
        }

        private async Task HandleButton(SocketMessageComponent component)
        {
            if (lastSearch is null) return;

            int tail = lastSearch.characters!.Count - (lastSearch.currentPage - 1) * 10;
            int maxRow = tail > 10 ? 10 : tail;

            switch (component.Data.CustomId)
            {
                case "up":
                    if (lastSearch.currentRow == 1)
                        lastSearch.currentRow = maxRow;
                    else
                        lastSearch.currentRow--;
                    break;
                case "down":
                    if (lastSearch.currentRow > maxRow)
                        lastSearch.currentRow = 1;
                    else 
                        lastSearch.currentRow++;
                    break;
                case "left":
                    lastSearch.currentRow = 1;

                    if (lastSearch.currentPage == 1)
                        lastSearch.currentPage = lastSearch.pages;
                    else
                        lastSearch.currentPage--;
                    break;
                case "right":
                    lastSearch.currentRow = 1;

                    if (lastSearch.currentPage == lastSearch.pages)
                        lastSearch.currentPage = 1;
                    else
                        lastSearch.currentPage++;
                    break;
                case "select":
                    var context = new SocketCommandContext(_client, component.Message);

                    using (context.Message.Channel.EnterTypingState())
                    {
                        int index = (lastSearch.currentPage - 1) * 10 + lastSearch.currentRow - 1;
                        var character = lastSearch.characters![index];
                        string charId = (string)character.external_id;
                        string charImg = $"https://characterai.io/i/400/static/avatars/{character.avatar_file_name}";

                        _ = CommandsService.SetCharacterAsync(charId, this, context);
                        await component.UpdateAsync(c =>
                        {
                            c.Embed = new EmbedBuilder()
                            {
                                Title = $"✅ Selected - {character.participant__name}",
                                Description = $"Original link: [Chat with {character.participant__name}](https://beta.character.ai/chat?char={charId})",
                                ImageUrl = charImg,
                                Footer = new EmbedFooterBuilder().WithText($"Created by {character.user__username}")
                            }.Build();
                            c.Components = null;
                        }).ConfigureAwait(false);
                    }
                    return;
                default:
                    return;
            }

            // If left/right/up/down is selected
            await component.UpdateAsync(c => c.Embed = BuildCharactersList(
                lastSearch.characters!, lastSearch.pages, lastSearch.query!,
                row: lastSearch.currentRow,
                page: lastSearch.currentPage
            )).ConfigureAwait(false);
        }

        private async Task UpdateMessageAsync(IUserMessage message)
        {
            dynamic? newReply = null;
            try { newReply = lastResponse.replies[lastResponse.currReply]; }
            catch
            {
                _ = message.ModifyAsync(msg => { msg.Content = $"( 🕓 Wait... )"; });
                var response = await integration.CallCharacter("", "", parentMsgId: lastResponse.lastUserMsgId);
                if (response is string) return;

                lastResponse.replies.Merge(response!.replies);
                newReply = lastResponse.replies[lastResponse.currReply];
            }

            lastResponse.primaryMsgId = newReply.id;
            string? replyImage = newReply?.image_rel_path;

            Embed? embed = null;
            if (replyImage != null && await TryGetImage(replyImage))
                embed = new EmbedBuilder().WithImageUrl(replyImage).Build();

            _ = message.ModifyAsync(msg => { msg.Content = $"{newReply!.text}"; msg.Embed = embed; }).ConfigureAwait(false);
        }

        private async Task CallCharacterAsync(SocketUserMessage message)
        {
            if (integration.charInfo.CharId == null)
            {
                await message.ReplyAsync("⚠ Set a character first").ConfigureAwait(false);
                return;
            }

            if (temps.lastCharacterCallMsgId != 0)
            {
                var lastMessage = await message.Channel.GetMessageAsync(temps.lastCharacterCallMsgId);
                _ = RemoveButtons(lastMessage);
            }

            string text = RemoveMention(message.Content);
            string imgPath = "";

            // Prepare call data
            if (integration.audienceMode)
                text = MakeItThreadMessage(text, message);
            if (message.Attachments.Any())
            {   // Downloads first image from attachments and uploads it to server
                string url = message.Attachments.First().Url;
                if (await TryDownloadImg(url, 10) is byte[] @img && await integration.UploadImg(@img) is string @path)
                    imgPath = $"https://characterai.io/i/400/static/user/{@path}";
            }

            // Send message to character
            var response = await integration.CallCharacter(text, imgPath, primaryMsgId: lastResponse.primaryMsgId);
            lastResponse.SetDefaults();

            // Alert with error message if call returns string
            if (response is string @string)
            {
                await message.ReplyAsync(@string).ConfigureAwait(false);
                return;
            }

            lastResponse.replies = response!.replies;
            lastResponse.lastUserMsgId = (string)response!.last_user_msg_id;

            // Take first character answer by default and reply with it
            var reply = lastResponse.replies[0];
            _ = Task.Run(async () => temps.lastCharacterCallMsgId = await ReplyOnMessage(message, reply));
        }

        private async Task<bool> UserIsBanned(SocketUserMessage message)
        {
            ulong currUser = message.Author.Id;
            var context = new SocketCommandContext(_client, message);
            if (temps.blackList.Contains(currUser)) return true;
            if (currUser == context.Guild.OwnerId) return false;

            int currMinute = message.CreatedAt.Minute + message.CreatedAt.Hour * 60;

            if (!temps.userMsgCount.ContainsKey(currUser))
            {
                temps.userMsgCount.Add(currUser, new ExpandoObject());
                temps.userMsgCount[currUser].minute = currMinute;
                temps.userMsgCount[currUser].count = 0;
            }

            if (temps.userMsgCount[currUser].minute != currMinute)
            {
                temps.userMsgCount[currUser].minute = currMinute;
                temps.userMsgCount[currUser].count = 0;
            }

            temps.userMsgCount[currUser].count++;
            if (temps.userMsgCount[currUser].count == Config.rateLimit)
                await message.ReplyAsync($"⚠ Warning! If you proceed to call {_client.CurrentUser.Mention} so fast," +
                                         " your messages will be ignored.");

            if (temps.userMsgCount[currUser].count > Config.rateLimit)
            {
                temps.blackList.Add(currUser);
                temps.userMsgCount.Remove(currUser);

                return true;
            }

            return false;
        }

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}