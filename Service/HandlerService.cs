using Discord.WebSocket;
using Discord;
using System.Text.RegularExpressions;
using CharacterAI.Models;
using CharacterAI_Discord_Bot.Models;
using Discord.Commands;
using CharacterAI;
using CharacterAI_Discord_Bot.Handlers;
using DeepL;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class HandlerService : CommonService
    {
        internal Integration CurrentIntegration { get; set; } = null!;
        internal Translator DeeplClient { get; set; } = null!;
        internal List<ulong> BlackList { get; set; } = new();
        internal List<TranslatedMessage> TranslatedMessages { get; set; } = new();
        internal List<DiscordChannel> Channels { get; set; } = new();
        internal LastSearchQuery? LastSearch { get; set; }
        internal Dictionary<ulong, int> RemoveEmojiRequestQueue { get; set; } = new();
        internal static Random @Random = new();

        internal static Emoji ARROW_LEFT = new("\u2B05");
        internal static Emoji ARROW_RIGHT = new("\u27A1");
        internal static Emoji STOP_BTN = new("\u26D4");
        internal static Emoji TRANSLATE_BTN = new("\uD83D\uDD24");
        //internal static Emoji REPEAT_BTN = new("\uD83D\uDD04");
        internal static string WAIT_MESSAGE = $"( 🕓 Wait... )";

        internal async Task<ulong> RespondOnMessage(SocketUserMessage message, Reply reply, bool isPrivate)
        {
            string replyText = reply.Text!;

            // (3 or more) "\n\n\n..." -> (exactly 1) "\n"
            replyText = LBRegex().Replace(replyText, "\n");

            // Discord won't accept message that is longer than 2000 symbols
            if (replyText.Length > 2000)
                replyText = replyText[0..1993] + " [...]";

            // Add image to the message
            Embed? embed = null;
            if (reply.HasImage && await TryGetImageAsync(reply.ImageRelPath!, @HttpClient))
                embed = new EmbedBuilder().WithImageUrl(reply.ImageRelPath).Build();

            // Send message
            var mentions = isPrivate ? AllowedMentions.None : AllowedMentions.All;
            var botReply = await message.ReplyAsync(replyText, embed: embed, allowedMentions: mentions).ConfigureAwait(false);

            bool isReplyToBot = botReply.ReferencedMessage is IUserMessage um && um.Author.IsBot;

            // If reply to user, add swipe arrows
            if (!isReplyToBot && BotConfig.SwipesEnabled)
            {
                await AddArrowButtonsAsync(botReply).ConfigureAwait(false);
                if (BotConfig.BtnsRemoveDelay > 0)
                    _ = RemoveButtonsAsync(botReply, delay: BotConfig.BtnsRemoveDelay);
            }
            // If reply to bot, add stop button
            if (isReplyToBot && BotConfig.StopBtnEnabled)
                await AddStopButtonAsync(botReply);

            if (BotConfig.TranslateBtnEnabled)
                await AddTranslateButtonAsync(botReply);

            return botReply!.Id;
        }

        internal async Task<DiscordChannel> StartTrackingChannelAsync(SocketCommandContext context)
        {
            var cI = CurrentIntegration;
            var charId = cI.CurrentCharacter.IsEmpty ? null : cI.CurrentCharacter.Id;
            var historyId = cI.CurrentCharacter.IsEmpty ? null : await cI.CreateNewChatAsync() ?? cI.Chats[0];

            var data = new ChannelData(charId, historyId);
            var currentChannel = new DiscordChannel(context.Channel.Id, context.User.Id, data)
            {
                ChannelName = context.Channel.Name,
                GuildId = context.Guild?.Id ?? 0,
                GuildName = context.Guild?.Name ?? "DM"
            };
            Channels.Add(currentChannel);
            SaveData(channels: Channels);

            return currentChannel;
        }

        internal async Task SelectCharacterAsync(CommandsHandler sender, SocketMessageComponent component, SocketCommandContext refContext)
        {
            int index = (LastSearch!.CurrentPage - 1) * 10 + LastSearch.CurrentRow - 1;
            var characterId = LastSearch.Response!.Characters![index].Id;
            var character = await CurrentIntegration.GetInfoAsync(characterId);
            if (character.IsEmpty) return;

            var typing = refContext.Channel.EnterTypingState();
            _ = SetCharacterAsync(characterId!, sender, refContext);
            await Task.Delay(2000);
            typing.Dispose();

            var imageUrl = TryGetImageAsync(character.AvatarUrlFull!, @HttpClient).Result ?
                character.AvatarUrlFull : TryGetImageAsync(character.AvatarUrlMini!, @HttpClient).Result ?
                character.AvatarUrlMini : null;

            var embed = new EmbedBuilder()
            {
                ImageUrl = imageUrl,
                Title = $"✅ Selected - {character.Name}",
                Footer = new EmbedFooterBuilder().WithText($"Created by {character.Author}"),
                Description = $"{character.Description}\n\n" +
                                $"*Original link: [Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})*\n" +
                                $"*Can generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}*\n" +
                                $"*Interactions: {character.Interactions}*"
            }.Build();

            try { await component.FollowupAsync(embed: embed, components: null); }
            catch (Exception e) { Failure(e.ToString(), client: refContext.Client); }
        }

        internal async Task TranslateMessageAsync(IUserMessage message, string translateTo)
        {
            // See if message was being translated already
            var tMessage = TranslatedMessages.Find(m => m.MessageId == message.Id);
            if (tMessage is null && DeeplClient is null) return;
            if (string.IsNullOrWhiteSpace(message.Content)) return;
            if (message.Content == WAIT_MESSAGE) return;

            // Add it to the list if was not
            if (tMessage is null)
            {
                TranslatedMessages.Add(new(message));
                tMessage = TranslatedMessages.Last();
            }

            int textIndex;
            string resultText;
            string originalText;
            
            if (tMessage.IsTranslated)
            {   // Restore original message
                textIndex = tMessage.LastTextIndex;
                originalText = tMessage.OriginalTexts.ElementAt(textIndex);
                resultText = originalText;
                tMessage.IsTranslated = false;
            }
            else
            {   // Translate message or restore translation
                textIndex = tMessage.OriginalTexts.FindIndex(text => text == message.Content); // not really safe, but ig it's fine
                if (textIndex == -1) // -1 == not found
                {
                    tMessage.OriginalTexts.Add(message.Content);
                    textIndex = tMessage.OriginalTexts.IndexOf(message.Content);
                }

                tMessage.TranslatedTexts.TryGetValue(textIndex, out string? translatedText);
                // If it never was translated, try to translate                
                if (translatedText is null)
                {
                    originalText = tMessage.OriginalTexts.ElementAt(textIndex);
                    await message.ModifyAsync(msg => { msg.Content = WAIT_MESSAGE; msg.AllowedMentions = AllowedMentions.None; }).ConfigureAwait(false);
                    translatedText = await TryToTranslateAsync(originalText, translateTo);
                    tMessage.TranslatedTexts.Add(textIndex, translatedText);
                }
                resultText = translatedText;
                tMessage.IsTranslated = true;
                tMessage.LastTextIndex = textIndex;
            }

            await message.ModifyAsync(m => m.Content = resultText).ConfigureAwait(false);
            if (TranslatedMessages.Count > 100) TranslatedMessages.RemoveRange(0, 10);
        }

        internal async Task<string> TryToTranslateAsync(string originalText, string lang)
        {
            var usage = await DeeplClient.GetUsageAsync();
            if (usage.AnyLimitReached) return "Translation limit exceeded.";
            
            try
            {
                var response = await DeeplClient.TranslateTextAsync(
                    text: originalText,
                    sourceLanguageCode: null,
                    targetLanguageCode: lang);

                return response.Text;
            }
            catch (Exception e)
            {
                Failure(e.ToString());
                return $"{WARN_SIGN_DISCORD} Failed to translate message.";
            }
        }

        internal static async Task AddTranslateButtonAsync(IUserMessage? message)
        {
            if (message is null) return;

            await message.AddReactionAsync(TRANSLATE_BTN).ConfigureAwait(false);
        }

        internal static async Task AddArrowButtonsAsync(IUserMessage? message)
        {
            if (message is null) return;

            var btns = new Emoji[] { ARROW_LEFT, ARROW_RIGHT };
            await message.AddReactionsAsync(btns).ConfigureAwait(false);
        }

        internal static async Task AddStopButtonAsync(IUserMessage? message)
        {
            if (message is null) return;

            await message.AddReactionAsync(STOP_BTN).ConfigureAwait(false);
        }

        //public static async Task AddRepeatButtonAsync(IUserMessage? message)
        //{
        //    if (message is null) return;

        //    await message.AddReactionAsync(REPEAT_BTN).ConfigureAwait(false);
        //}

        internal async Task RemoveButtonsAsync(IMessage lastMessage, int delay = 0)
        {
            // Add request to the end of the line
            ulong key = lastMessage.Id;
            RemoveEmojiRequestQueue.Add(key, value: delay);
            // Delay it till it will take the first place
            while (RemoveEmojiRequestQueue.First().Key != key)
                await Task.Delay(200);

            // Wait for actual "remove delay" to become 0
            // Delay can be updated outside of this method
            while (RemoveEmojiRequestQueue[key] > 0)
            {
                await Task.Delay(1000);
                RemoveEmojiRequestQueue[key]--;
            }
            // May fail because of permissions or some connection problems
            await TryToRemoveEmojisAsync(lastMessage);
            // Remove from the line
            RemoveEmojiRequestQueue.Remove(key);
        }

        private static async Task TryToRemoveEmojisAsync(IMessage message)
        {
            try { // don't look here, I know it's horrible
                foreach (var btn in new Emoji[] { ARROW_LEFT, ARROW_RIGHT, STOP_BTN })
                    await message.RemoveReactionAsync(btn, message.Author).ConfigureAwait(false); }
            catch { }
        }

        /// <summary>
        /// Remove prefix and/or @mention_prefix
        /// </summary>
        internal static string RemoveMention(string text)
        {
            text = text.Trim();
            // Remove first @mention
            if (text.StartsWith("<"))
                text = new Regex("\\<(.*?)\\>").Replace(text, "", 1);
            // Remove prefix
            var prefixes = BotConfig.BotPrefixes.OrderBy(p => p.Length).Reverse().ToArray(); // ex: "~ai" first, only then "~"
            foreach (string prefix in prefixes)
                if (text.StartsWith(prefix))
                    text = text.Replace(prefix, "");

            return text;
        }

        internal static string AddUsername(string text, SocketCommandContext context)
        {
            string name;
            if (context.Guild is null)
                name = context.User.Username;
            else
            {
                var guildUser = context.User as SocketGuildUser;
                name = guildUser?.Nickname ?? guildUser!.Username;
            }

            string username = BotConfig.AudienceModeNameFormat.Replace("{username}", name);
            if (!string.IsNullOrWhiteSpace(text))
                text = username + text;

            return text;
        }

        internal static string AddQuote(string text, SocketUserMessage message)
        {
            var refMsg = message.ReferencedMessage;
            bool hasRefMessage = refMsg is not null && !string.IsNullOrEmpty(refMsg.Content);
            if (hasRefMessage)
            {
                string refContent = refMsg!.Content;
                if (refContent.Length > 200) refContent = refContent[0..197] + "...";
                string quote = BotConfig.AudienceModeQuoteFormat.Replace("{quote}", refContent);
                text = quote + RemoveMention(text);
            }

            return text;
        }


        [GeneratedRegex("(\\n){3,}")]
        private static partial Regex LBRegex();
    }
}
