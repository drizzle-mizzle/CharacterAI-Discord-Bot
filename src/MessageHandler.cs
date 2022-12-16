using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CharacterAI_Discord_Bot
{
    public class MessageHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;
        private Integration? _integration;
        

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
            if (!message.HasMentionPrefix(_client.CurrentUser, ref argPos) && 
                 !(message.ReferencedMessage != null && message.ReferencedMessage.Author.Id == _client.CurrentUser.Id)) return;
            // execute and return if message was a command
            string text = RemoveMention(message.Content);
            if (text.StartsWith("!")) { await HandleCommandAsync(text, context); return; }

            // return if try to call a character before it was set up
            if (_integration._charInfo.CharID == null) { await message.ReplyAsync("⚠ Set a character first"); return; }

            using (message.Channel.EnterTypingState())
            {
                if (_integration._audienceMode)
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
                try { await context.Message.Channel.SendFileAsync(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "nopower.gif"); }
                catch { Console.WriteLine("\nPut that damn .gif back!\n"); };

                return;
            }

            Commands commands = new(_client, _integration);
            var cmdArg = text.Split(" ");

            switch (cmdArg[0])
            {
                case "!set-character":
                    string arg = "";
                    if (text.Split(" ").Length > 1) arg = cmdArg[1];

                    await commands.SetCharacter(arg, context); break;
                case "!audience-toggle":
                    await commands.AudienceToggle(context); break;
                case "!ping":
                    await commands.Ping(context); break;
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
