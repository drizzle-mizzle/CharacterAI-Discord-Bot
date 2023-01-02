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
        public readonly Integration integration;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;
        private readonly dynamic _lastResponse;
        private string? _lastCharacterCallMsgId;

        public CommandHandler(IServiceProvider services)
        {
            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();

            integration = new Integration(Config.userToken);

            _lastResponse = new ExpandoObject();
            _lastResponse.SetDefaults = (Action)(() =>
            {
                _lastResponse.replies = (dynamic?)null;
                _lastResponse.currReply = 0;
                _lastResponse.primaryMsgId = 0;
                _lastResponse.lastUserMsgId = 0;
            });
            _lastResponse.SetDefaults();

            _client.MessageReceived += HandleMessage;
            _client.ReactionAdded += HandleReaction;
            _client.ReactionRemoved += HandleReaction;
        }

        private Task HandleMessage(SocketMessage rawMessage)
        {
            var message = rawMessage as SocketUserMessage;
            if (message is null) return Task.CompletedTask;

            int argPos = 0;
            string[] prefixes = Config.botPrefixes;

            bool hasMention = message.HasMentionPrefix(_client.CurrentUser, ref argPos);
            bool hasPrefix = !hasMention && prefixes.Any(p => message.HasStringPrefix(p, ref argPos));
            bool hasReply = !hasPrefix && !hasMention && message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id; // SO FUCKING BIG UUUGHH!

            if (hasMention || hasPrefix || hasReply)
            {
                var context = new SocketCommandContext(_client, message);
                var cmdResponse = _commands.ExecuteAsync(context, argPos, _services).Result;

                if (!cmdResponse.IsSuccess)
                    using (message.Channel.EnterTypingState())
                        Task.Run(() => CallCharacterAsync(message));
            }

            return Task.CompletedTask;
        }

        private Task HandleReaction(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (_lastResponse.replies is null || rawMessage.Id.ToString() != _lastCharacterCallMsgId)
                return Task.CompletedTask;

            var message = rawMessage.DownloadAsync().Result;
            var user = reaction.User.Value as SocketGuildUser;
            if (user!.IsBot || user.Id != message.ReferencedMessage.Author.Id)
                return Task.CompletedTask;

            if (reaction.Emote.Name == new Emoji("\u27A1").Name)
            {   // right arrow
                _lastResponse.currReply++;
                Task.Run(() => UpdateMessageAsync(message));
            }
            else if (reaction.Emote.Name == new Emoji("\u2B05").Name && _lastResponse.currReply > 0)
            {   // left arrow
                _lastResponse.currReply--;
                Task.Run(() => UpdateMessageAsync(message));
            }

            return Task.CompletedTask;
        }

        private async Task UpdateMessageAsync(IUserMessage message)
        {
            dynamic? newReply = null;
            try { newReply = _lastResponse.replies[_lastResponse.currReply]; }
            catch
            {
                await message.ModifyAsync(msg => { msg.Content = $"( 🕓 Wait... )"; });
                var response = await integration.CallCharacter("", "", parentMsgId: _lastResponse.lastUserMsgId);
                if (response is string) return;

                _lastResponse.replies.Merge(response!.replies);
                newReply = _lastResponse.replies[_lastResponse.currReply];
            }

            _lastResponse.primaryMsgId = (int)newReply.id;

            if (newReply.image_rel_path == null)
                await message.ModifyAsync(msg => { msg.Content = $"{newReply.text}"; });
            else
            {   // There's no way to modify attachments in discord messages
                var refMsg = message.ReferencedMessage as SocketUserMessage;
                await message.DeleteAsync(); // so we just delete it and send a new one
                _lastCharacterCallMsgId = await ReplyOnMessage(refMsg, newReply);
            }
        }

        private async Task CallCharacterAsync(SocketUserMessage message)
        {
            if (integration.charInfo.CharId == null) { await message.ReplyAsync("⚠ Set a character first"); return; }

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
            var response = await integration.CallCharacter(text, imgPath, primaryMsgId: _lastResponse.primaryMsgId);
            _lastResponse.SetDefaults();

            // Alert with error message if call returns string
            if (response is string) { await message.ReplyAsync((string)response); return; }

            _lastResponse.replies = response!.replies;
            _lastResponse.lastUserMsgId = (int)response!.last_user_msg_id;

            // Take first character answer by default and reply with it
            var reply = _lastResponse.replies[0];
            _lastCharacterCallMsgId = await ReplyOnMessage(message, reply);
        }

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}