using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using CharacterAI_Discord_Bot.Handlers;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class CommandsService : CommonService
    {
        public static async Task AutoSetup(ServiceProvider services, DiscordSocketClient client)
        {
            var integration = services.GetRequiredService<CommandsHandler>().integration;
            bool result = await integration.Setup(Config.autoCharID, reset: false);
            if (!result) return;

            integration.audienceMode = Config.defaultAudienceMode;

            await UpdatePlayingStatus(integration, client).ConfigureAwait(false);
            await SetBotNickname(integration.charInfo, client).ConfigureAwait(false);
            await SetBotAvatar(client.CurrentUser).ConfigureAwait(false);
        }

        public static async Task<Task> FindCharacterAsync(string query, CommandsHandler handler, SocketCommandContext context)
        {
            return Task.CompletedTask;
            //var integration = handler.integration;
            //var characters = await integration.Search(query);
            //if (characters is null)
            //    return Task.Run(() => context.Message.ReplyAsync("⚠️ No characters were found"));

            //var btns = new ComponentBuilder()
            //    .WithButton(emote: new Emoji("\u2B06"), label: "up", style: ButtonStyle.Secondary)
            //    .WithButton(emote: new Emoji("\u2B07"), label: "down", style: ButtonStyle.Secondary)
            //    .WithButton(emote: new Emoji("\u2705"), label: "select", style: ButtonStyle.Success);

            //if (characters.Count > 10)
            //    btns.WithButton(emote: new Emoji("\u2B05"), label: "left", style: ButtonStyle.Secondary)
            //        .WithButton(emote: new Emoji("\u27A1"), label: "right", style: ButtonStyle.Secondary);

            //var list = new EmbedBuilder().WithTitle($"Characters found by query \"{query}\":")
            //    .WithFooter($"Page 1/{characters.Count}");

            //foreach (var character in characters)
            //{
            //    list.AddField()
            //}

            //await context.Message.ReplyAsync(embed: list.Build());
        }

        public static async Task<Task> SetCharacterAsync(string charID, CommandsHandler handler, SocketCommandContext context, bool reset = false)
        {
            var integration = handler.integration;

            if (handler.lastCharacterCallMsgId != 0)
            {
                var lastMessage = await context.Message.Channel.GetMessageAsync(handler.lastCharacterCallMsgId);
                await HandlerService.RemoveButtons(lastMessage).ConfigureAwait(false);
                handler.lastResponse.SetDefaults();
            }

            if (await integration.Setup(charID, reset: reset) is false)
                return Task.Run(() => context.Message.ReplyAsync("⚠️ Failed to set character!"));

            var charInfo = integration.charInfo;
            string reply = $"{charInfo.Greeting}\n*\"{charInfo.Description}\"*";

            try { await SetBotNickname(charInfo, context.Client).ConfigureAwait(false); }
            catch { reply += "\n⚠️ Failed to set bot name! Probably, missing permissions?"; }

            try { await SetBotAvatar(context.Client.CurrentUser).ConfigureAwait(false); }
            catch { reply += "\n⚠️ Failed to set bot avatar!"; }

            return Task.Run(() =>
            {
                UpdatePlayingStatus(integration, context.Client).ConfigureAwait(false);
                context.Message.ReplyAsync(reply);
            });
        }

        public static async Task ResetCharacterAsync(CommandsHandler handler, SocketCommandContext context)
        {
            string charId = handler.integration.charInfo.CharId!;
            await Task.Run(()
                => SetCharacterAsync(charId, handler, context, reset: true)).ConfigureAwait(false);
        }

        public static async Task SetBotNickname(Character charInfo, DiscordSocketClient client)
        {
            var guildID = client.Guilds.First().Id;
            var botAsGuildUser = client.GetGuild(guildID).GetUser(client.CurrentUser.Id);
            await botAsGuildUser.ModifyAsync(u => { u.Nickname = charInfo.Name; });
        }

        public static async Task SetBotAvatar(SocketSelfUser bot)
        {
            using var fs = new FileStream(avatarPath, FileMode.Open);
            await bot.ModifyAsync(u => { u.Avatar = new Discord.Image(fs); });
        }

        public static async Task UpdatePlayingStatus(Integration integration, DiscordSocketClient client)
        {
            bool amode = integration.audienceMode;
            var charInfo = integration.charInfo;

            string desc = charInfo.CharId is null ? "No character selected" : charInfo.Title;
            await client.SetGameAsync($"Audience mode: " + (amode ? "✅" : "❌") + " | " + desc);
        }

        public static Task NoPermissionAlert(SocketCommandContext context)
        {
            if (string.IsNullOrEmpty(nopowerPath) || !File.Exists(nopowerPath))
                return Task.CompletedTask;
            
            var mRef = new MessageReference(context.Message.Id);
            Task.Run(() => context.Channel.SendFileAsync(nopowerPath, messageReference: mRef));

            return Task.CompletedTask;
        }

        public static bool ValidateBotRole(SocketCommandContext context)
        {
            var user = context.User as SocketGuildUser;
            if (user!.Id == context.Guild.OwnerId) return true;

            var roles = (user as IGuildUser).Guild.Roles;
            var requiredRole = roles.FirstOrDefault(role => role.Name == Config.botRole);

            return user.Roles.Contains(requiredRole);
        }
    }
}
