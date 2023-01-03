using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Dynamic;
using CharacterAI_Discord_Bot.Service;

namespace CharacterAI_Discord_Bot.Handlers
{
    public class CommandHandler : HandlerService
    {
        public int replyChance = 0;
        public int skipMessages = 0;
        public List<ulong> huntedUsers = new();
        public ulong lastCharacterCallMsgId = 0;
        public readonly Integration integration;
        public readonly dynamic lastResponse;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;

        public CommandHandler(IServiceProvider services)
        {
            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();

            integration = new Integration(Config.userToken);

            lastResponse = new ExpandoObject();
            lastResponse.SetDefaults = (Action)(() =>
            {
                lastResponse.replies = (dynamic?)null;
                lastResponse.currReply = 0;
                lastResponse.primaryMsgId = 0;
                lastResponse.lastUserMsgId = 0;
            });
            lastResponse.SetDefaults();

            _client.MessageReceived += HandleMessage;
            _client.ReactionAdded += HandleReaction;
            _client.ReactionRemoved += HandleReaction;
        }

        private Task HandleMessage(SocketMessage rawMessage)
        {
            if (rawMessage is not SocketUserMessage message || message.Author.Id == _client.CurrentUser.Id)
                return Task.CompletedTask;

            int argPos = 0;
            string[] prefixes = Config.botPrefixes;
            var RandomGen = new Random();

            bool hasMention = message.HasMentionPrefix(_client.CurrentUser, ref argPos);
            bool hasPrefix = !hasMention && prefixes.Any(p => message.HasStringPrefix(p, ref argPos));
            bool hasReply = !hasPrefix && !hasMention && message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id; // SO FUCKING BIG UUUGHH!
            bool randomReply = replyChance >= RandomGen.Next(100);
            bool huntedUser = huntedUsers.Contains(message.Author.Id);

            if (hasMention || hasPrefix || hasReply || huntedUser || randomReply)
            {
                var context = new SocketCommandContext(_client, message);
                var cmdResponse = _commands.ExecuteAsync(context, argPos, _services).Result;

                if (!cmdResponse.IsSuccess)
                {
                    if (skipMessages > 0)
                        skipMessages--;
                    else
                        using (message.Channel.EnterTypingState())
                            Task.Run(() => CallCharacterAsync(message));
                }
            }

            return Task.CompletedTask;
        }

        private Task HandleReaction(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (lastResponse.replies is null || rawMessage.Id.ToString() != lastCharacterCallMsgId.ToString())
                return Task.CompletedTask;

            var message = rawMessage.DownloadAsync().Result;
            var user = reaction.User.Value as SocketGuildUser;
            if (user!.IsBot || user.Id != message.ReferencedMessage.Author.Id)
                return Task.CompletedTask;

            if (reaction.Emote.Name == new Emoji("\u27A1").Name)
            {   // right arrow
                lastResponse.currReply++;
                Task.Run(() => UpdateMessageAsync(message));
            }
            else if (reaction.Emote.Name == new Emoji("\u2B05").Name && lastResponse.currReply > 0)
            {   // left arrow
                lastResponse.currReply--;
                Task.Run(() => UpdateMessageAsync(message));
            }

            return Task.CompletedTask;
        }

        private async Task UpdateMessageAsync(IUserMessage message)
        {
            dynamic? newReply = null;
            try { newReply = lastResponse.replies[lastResponse.currReply]; }
            catch
            {
                await message.ModifyAsync(msg => { msg.Content = $"( 🕓 Wait... )"; }).ConfigureAwait(false);
                var response = await integration.CallCharacter("", "", parentMsgId: lastResponse.lastUserMsgId);
                if (response is string) return;

                lastResponse.replies.Merge(response!.replies);
                newReply = lastResponse.replies[lastResponse.currReply];
            }

            lastResponse.primaryMsgId = (int)newReply.id;

            if (newReply.image_rel_path == null)
                await message.ModifyAsync(msg => { msg.Content = $"{newReply.text}"; }).ConfigureAwait(false);
            else
            {   // There's no way to modify attachments in discord messages
                var refMsg = message.ReferencedMessage as SocketUserMessage;
                await message.DeleteAsync().ConfigureAwait(false); // so we just delete it and send a new one
                lastCharacterCallMsgId = await ReplyOnMessage(refMsg, newReply);
            }
        }

        private async Task<Task> CallCharacterAsync(SocketUserMessage message)
        {
            if (integration.charInfo.CharId == null)
                return Task.Run(() => message.ReplyAsync("⚠ Set a character first"));

            await RemoveButtons(lastCharacterCallMsgId, message).ConfigureAwait(false);
            string text = RemoveMention(message.Content);
            string imgPath = "";

            // Prepare call data
            if (integration.audienceMode)
                text = MakeItThreadMessage(text, message);
            if (message.Attachments.Any())
            {   // Downloads first image from attachments and uploads it to server
                string url = message.Attachments.First().Url;
                if (await DownloadImg(url) is byte[] img && integration.UploadImg(img) is Task<string> path)
                    imgPath = $"https://characterai.io/i/400/static/user/{path}";
            }

            // Send message to character
            var response = await integration.CallCharacter(text, imgPath, primaryMsgId: lastResponse.primaryMsgId);
            lastResponse.SetDefaults();

            // Alert with error message if call returns string
            if (response is string @string)
                return Task.Run(() => message.ReplyAsync(@string));
            
            lastResponse.replies = response!.replies;
            lastResponse.lastUserMsgId = (int)response!.last_user_msg_id;

            // Take first character answer by default and reply with it
            var reply = lastResponse.replies[0];
            lastCharacterCallMsgId = await ReplyOnMessage(message, reply);

            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}