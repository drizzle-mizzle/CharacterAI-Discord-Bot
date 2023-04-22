using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CharacterAI_Discord_Bot.Handlers;
using CharacterAI_Discord_Bot.Models;

namespace CharacterAI_Discord_Bot.Service
{
    /// <summary>
    /// Contains all 'first-layer underhood logic' for executable commands.
    /// </summary>
    public partial class CommandsService : CommonService
    {
        // [Command("find character")]
        public static async Task FindCharacterAsync(string query, CommandsHandler handler, SocketCommandContext context)
        {
            var integration = handler.CurrentIntegration;
            var response = await integration.SearchAsync(query);

            if (response.IsEmpty)
            {
                await context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} No characters were found").ConfigureAwait(false);
                return;
            }

            int pages = (int)Math.Ceiling((float)response.Characters.Count / 10);

            // List navigation buttons
            var buttons = new ComponentBuilder()
                .WithButton(emote: new Emoji("\u2B06"), customId: "up", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2B07"), customId: "down", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2705"), customId: "select", style: ButtonStyle.Success);
            // Pages navigation buttons
            if (pages > 1) buttons
                .WithButton(emote: new Emoji("\u2B05"), customId: "left", row: 1)
                .WithButton(emote: new Emoji("\u27A1"), customId: "right", row: 1);

            handler.LastSearch = new LastSearchQuery(response) { Pages = pages, Query = query };

            var list = BuildCharactersList(handler.LastSearch);
            await context.Message.ReplyAsync(embed: list, components: buttons.Build()).ConfigureAwait(false);
        }

        // [Command("reset character")]
        public static async Task ResetCharacterAsync(CommandsHandler handler, SocketCommandContext context)
        {
            var cI = handler.CurrentIntegration;
            var newHistoryId = await cI.CreateNewChatAsync();
            if (newHistoryId is null) return;

            var currentChannel = handler.Channels.Find(c => c.Id == context.Channel.Id);

            var data = new CharacterDialogData(cI.CurrentCharacter.Id!, newHistoryId);
            var newChannel = new DiscordChannel(context.Channel.Id, context.User.Id, data);

            if (currentChannel is not null)
            {
                newChannel.Data.AudienceMode = currentChannel.Data.AudienceMode;
                newChannel.Data.ReplyChance = currentChannel.Data.ReplyChance;
                newChannel.Data.ReplyDelay = currentChannel.Data.ReplyDelay;
                newChannel.Data.SkipMessages = currentChannel.Data.SkipMessages;
                newChannel.GuestsList = currentChannel.GuestsList;

                handler.Channels.Remove(currentChannel);
            }

            handler.Channels.Add(newChannel);
            SaveData(handler.Channels);

            await context.Message.ReplyAsync(handler.CurrentIntegration.CurrentCharacter.Greeting!);
        }

        // [Command("private")]
        public static async Task CreatePrivateChatAsync(CommandsHandler handler, SocketCommandContext context)
        {
            var cI = handler.CurrentIntegration;
            var newChatHistoryId = await cI.CreateNewChatAsync();
            if (newChatHistoryId is null)
            {
                await context.Message.ReplyAsync("Failed to create a new chat!");
                return;
            }

            ulong categoryId = await FindOrCreateCategoryAsync(context, BotConfig.Category);
            var newChannel = await CreateChannelAsync(context, categoryId, cI);
            var infoMsg = await newChannel.SendMessageAsync($"History Id: **`{newChatHistoryId}`**\n" +
                                                            $"Created by: {context.User.Mention}\n" +
                                                            $"Use **`add {newChannel.Id} @user`** to add other users to this channel.\n" +
                                                            $"Use **`kick @user`** to kick user from this channel.");
            await infoMsg.PinAsync();
            await newChannel.SendMessageAsync(cI.CurrentCharacter.Greeting);

            bool isOwner = context.User.Id == context.Guild.OwnerId;
            bool isManager = isOwner || ((SocketGuildUser)context.User).Roles.Any(r => r.Name == BotConfig.BotRole);
            if (!isManager) // owner's and managers' channels will not be deactivated
                DeactivateOldUserChannel(handler, context.User.Id);

            // Update channels list
            var data = new CharacterDialogData(cI.CurrentCharacter.Id!, newChatHistoryId) { AudienceMode = 0 };
            var newChannelItem = new DiscordChannel(newChannel.Id, context.User.Id, data);
            handler.Channels.Add(newChannelItem);

            SaveData(channels: handler.Channels);
        }

        public static Task NoPermissionAlert(SocketCommandContext context)
        {
            if (string.IsNullOrEmpty(nopowerPath) || !File.Exists(nopowerPath)) return Task.CompletedTask;
            
            var mRef = new MessageReference(context.Message.Id);
            _ = context.Channel.SendFileAsync(nopowerPath, messageReference: mRef).ConfigureAwait(false);

            return Task.CompletedTask;
        }

        public static bool ValidateUserAccess(SocketCommandContext context)
        {
            if (context.Guild is null) throw new Exception("Not a guild channel.");

            var user = context.User as SocketGuildUser;
            if (user!.Id == context.Guild.OwnerId) return true;

            var roles = (user as IGuildUser).Guild.Roles;
            var requiredRole = roles.FirstOrDefault(role => role.Name == BotConfig.BotRole);

            return user.Roles.Contains(requiredRole);
        }

        public static bool ValidatePublic(SocketCommandContext context)
        {
            if (!BotConfig.PublicMode) return true;
            if (BotConfig.HosterDiscordId is null) return false;

            return context.Message.Author.Id == BotConfig.HosterDiscordId;
        }
    }
}
