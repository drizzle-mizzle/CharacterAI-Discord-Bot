using CharacterAI_Discord_Bot.Service;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;


namespace CharacterAI_Discord_Bot
{
    public class MessageHandler : CommonService
    {
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly Integration _integration;
        private readonly CommandService _commands;
        
        public MessageHandler(IServiceProvider services)
        {
            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _integration = new Integration(Program.GetConfig().userToken);
            _client.MessageReceived += HandleMessageAsync;
        }

        public async Task HandleMessageAsync(SocketMessage rawMessage)
        {
            int argPos = 0;
            var message = rawMessage as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);

            // return if no mention and no reply
            if (message != null && !message.HasMentionPrefix(_client.CurrentUser, ref argPos) && 
                 !(message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id)) return;

            // execute and return if message was a command
            string text = RemoveMention(message.Content);
            if (text.StartsWith("!")) { await HandleCommandAsync(text, context); return; }

            // return if try to call a character before it was set up
            if (_integration._charInfo.CharID == null) { await message.ReplyAsync("⚠ Set a character first"); return; }

            using (message.Channel.EnterTypingState())
            {
                if (_integration.audienceMode)
                {
                    // Test feature that makes character aware that he's talking to many different people
                    string author = message.Author.Username;
                    text = $"User [{author}] says:\n{text}";
                    if (message.ReferencedMessage != null)
                        text = $"(In response to: \"{RemoveMention(message.ReferencedMessage.Content)}\")\n{text}";
                }
                string reply = _integration.CallCharacter(text);
                await message.ReplyAsync($"{message.Author.Mention} {reply}");
            }

            return;
        }

        public async Task HandleCommandAsync(string text, SocketCommandContext context)
        {
            if (context.Message.Author.Id != context.Guild.OwnerId)
            {
                var path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "nopower.gif";
                try { var result = await context.Message.Channel.SendFileAsync(path); }
                catch { Log("Cant' send file. Missing file or permission.", ConsoleColor.Red); }

                return;
            }

            Commands commands = new(_client, _integration);
            var cmdArg = text.Split(' ');

            switch (cmdArg[0])
            {
                case "!set-character" or "!set":
                    string arg = cmdArg.ElementAtOrDefault(1);
                    await commands.SetCharacter(arg, context); break;
                case "!audience-toggle" or "!au":
                    await commands.AudienceToggle(context); break;
                case "!ping":
                    await Commands.Ping(context); break;
            }
        }

        private static string RemoveMention(string text)
        {
            var rgx = new Regex(@"\<(.*?)\>");
            text = rgx.Replace(text, "", 1).Trim(' ');

            return text;
        }

        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}
//{ 
//    "replies": 
//    [
//        { "text": "", "id": 1042042806}, 
//        { "text": "", "id": 1042042800}
//    ], 
//    "src_char": 
//    {
//        "participant": { "name": "Rei Ayanami"}, 
//        "avatar_file_name": "uploaded/2022/11/21/NCqKYK9ccWBBhoUPnRthu_2rLgW_QeOT5Y-Vs-wIGrM.webp"
//    }, 
//    "is_final_chunk": true, 
//    "last_user_msg_id": 1042042788
//}