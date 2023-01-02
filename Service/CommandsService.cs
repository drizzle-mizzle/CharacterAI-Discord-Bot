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
            if (Config is null) return;

            var integration = services.GetRequiredService<CommandHandler>().integration;
            integration.audienceMode = Config.defaultAudienceMode;
            if (!await integration.Setup(Config.autoCharID)) return;

            var charInfo = integration.charInfo;

            await UpdatePlayingStatus(integration.charInfo, client, integration.audienceMode);
            await SetBotNickname(charInfo, client);
            await SetBotAvatar(client.CurrentUser);
        }

        public static async Task SetCharacterAsync(string charID, Integration integration, SocketCommandContext context)
        {
            if (!await integration.Setup(charID)) { await context.Message.ReplyAsync("⚠️ Failed to set character!"); return; }

            var charInfo = integration.charInfo;
            string reply = $"{charInfo.Greeting}\n*\"{charInfo.Description}\"*";

            try { await SetBotNickname(charInfo, context.Client); }
            catch { reply += "\n⚠️ Failed to set bot name! Probably, missing permissions?"; }

            try { await SetBotAvatar(context.Client.CurrentUser); }
            catch { reply += "\n⚠️ Failed to set bot avatar!"; }

            await UpdatePlayingStatus(charInfo, context.Client, integration.audienceMode);
            await context.Message.ReplyAsync(reply);
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

        public static async Task UpdatePlayingStatus(Character charInfo, DiscordSocketClient client, bool amode)
        {
            string? charID = charInfo.CharId;
            string desc = charID is null ? "No character selected" : charInfo.Title!;

            await client.SetGameAsync($"Audience mode: " + (amode ? "✔️" : "✖️") + " | " + desc);
        }

        public static bool ValidateBotRole(SocketCommandContext context)
        {
            var user = context.User as SocketGuildUser;
            if (user!.Id == context.Guild.OwnerId) return true;

            var roles = (user as IGuildUser).Guild.Roles;
            var requiredRole = roles.FirstOrDefault(role => role.Name == Config.botRole);

            return user.Roles.Contains(requiredRole);
        }

        public static void NoPermissionAlert(SocketCommandContext context)
        {
            if (!string.IsNullOrEmpty(nopowerPath) || File.Exists(nopowerPath))
            {
                var mRef = new MessageReference(messageId: context.Message.Id);
                Task.Run(() => context.Channel.SendFileAsync(nopowerPath, messageReference: mRef));
            }
        }

    }
}
