using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using CharacterAI_Discord_Bot.Handlers;
using CharacterAI_Discord_Bot.Models;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class CommandsService : CommonService
    {
        public static async Task AutoSetup(ServiceProvider services, DiscordSocketClient client)
        {
            var integration = services.GetRequiredService<CommandsHandler>().integration;
            bool result = await integration.Setup(Config.autoCharID, reset: false);
            if (result is false) return;

            integration.audienceMode = Config.defaultAudienceMode;

            _ = SetBotAvatar(client.CurrentUser, integration.charInfo.AvatarUrl!).ConfigureAwait(false);
            await UpdatePlayingStatus(integration, client).ConfigureAwait(false);
            await SetBotNickname(integration.charInfo, client).ConfigureAwait(false);
        }

        public static async Task SetCharacterAsync(string charID, CommandsHandler handler, SocketCommandContext context, bool reset = false)
        {
            var integration = handler.integration;

            if (handler.temps.lastCharacterCallMsgId != 0)
            {
                var lastMessage = await context.Message.Channel.GetMessageAsync(handler.temps.lastCharacterCallMsgId);
                _ = HandlerService.RemoveButtons(lastMessage);
                handler.lastResponse.SetDefaults();
            }

            if (await integration.Setup(charID, reset: reset) is false)
            {
                await context.Message.ReplyAsync("⚠️ Failed to set character!").ConfigureAwait(false);
                return;
            }

            var charInfo = integration.charInfo;
            string reply = charInfo.Greeting + (string.IsNullOrEmpty(charInfo.Description.Trim(' ')) ?
                "" : $"\n*\"{charInfo.Description}\"*");

            _ = UpdatePlayingStatus(integration, context.Client).ConfigureAwait(false);

            try { await SetBotNickname(charInfo, context.Client).ConfigureAwait(false); }
            catch { reply += "\n⚠️ Failed to set bot name! Probably, missing permissions?"; }

            try { await SetBotAvatar(context.Client.CurrentUser, charInfo.AvatarUrl!).ConfigureAwait(false); }
            catch { reply += "\n⚠️ Failed to set bot avatar!"; }

            await context.Message.ReplyAsync(reply).ConfigureAwait(false);
        }

        public static async Task FindCharacterAsync(string query, CommandsHandler handler, SocketCommandContext context)
        {
            var integration = handler.integration;
            var characters = await integration.Search(query);

            if (characters is null)
            {
                await context.Message.ReplyAsync("⚠️ No characters were found").ConfigureAwait(false);
                return;
            }

            int pages = (int)Math.Ceiling((float)characters.Count / 10);

            var buttons = new ComponentBuilder()
                .WithButton(emote: new Emoji("\u2B06"), customId: "up", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2B07"), customId: "down", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2705"), customId: "select", style: ButtonStyle.Success);
            // Page selection buttons
            if (pages > 1) buttons
                .WithButton(emote: new Emoji("\u2B05"), customId: "left", row: 1)
                .WithButton(emote: new Emoji("\u27A1"), customId: "right", row: 1);

            handler.lastSearch = new LastSearch() { pages = pages, characters = characters, query = query };

            
            var list = BuildCharactersList(characters, pages, query, row: 1, page: 1);
            await context.Message.ReplyAsync(embed: list, components: buttons.Build()).ConfigureAwait(false);
        }

        public static Task ResetCharacter(CommandsHandler handler, SocketCommandContext context)
        {
            string charId = handler.integration.charInfo.CharId!;
            _ = SetCharacterAsync(charId, handler, context, reset: true);
            
            return Task.CompletedTask;
        }

        public static async Task SetBotNickname(Character charInfo, DiscordSocketClient client)
        {
            var guildID = client.Guilds.First().Id;
            var botAsGuildUser = client.GetGuild(guildID).GetUser(client.CurrentUser.Id);

            await botAsGuildUser.ModifyAsync(u => { u.Nickname = charInfo.Name; }).ConfigureAwait(false);
        }

        public static async Task SetBotAvatar(SocketSelfUser bot, string url)
        {
            Stream image;
            var response = await TryDownloadImg(url, 1);
            if (response is not null)
                image = new MemoryStream(response);
            else
            {
                Log($"Failed to set bot avatar\n", ConsoleColor.Magenta);
                Log(" Setting default avatar... ", ConsoleColor.DarkCyan);

                try
                {
                    image = new FileStream(defaultAvatarPath, FileMode.Open);
                }
                catch (Exception e)
                {
                    Failure($"Something went wrong.\n" + e.ToString());
                    return;
                }
                Success("OK");
            }

            await bot.ModifyAsync(u => { u.Avatar = new Discord.Image(image); }).ConfigureAwait(false);
        }

        public static async Task UpdatePlayingStatus(Integration integration, DiscordSocketClient client)
        {
            var charInfo = integration.charInfo;
            bool amode = integration.audienceMode;
            string desc = charInfo.CharId is null ? "No character selected" : charInfo.Title;

            await client.SetGameAsync($"Audience mode: " + (amode ? "✅" : "❌") + " | " + desc).ConfigureAwait(false);
        }

        public static Task NoPermissionAlert(SocketCommandContext context)
        {
            if (string.IsNullOrEmpty(nopowerPath) || !File.Exists(nopowerPath)) return Task.CompletedTask;
            
            var mRef = new MessageReference(context.Message.Id);
            _ = context.Channel.SendFileAsync(nopowerPath, messageReference: mRef).ConfigureAwait(false);

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
