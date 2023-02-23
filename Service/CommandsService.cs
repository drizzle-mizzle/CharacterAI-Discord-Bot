using Discord;
using Discord.Rest;
using Discord.Commands;
using Discord.WebSocket;
using CharacterAI_Discord_Bot.Handlers;
using CharacterAI_Discord_Bot.Models;
using CharacterAI;
using CharacterAI.Models;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class CommandsService : CommonService
    {
        public static async Task SetCharacterAsync(string charId, CommandsHandler handler, SocketCommandContext context, bool reset = false)
        {
            var cI = handler.CurrentIntegration;
            var result = await cI.SetupAsync(charId, startWithNewChat: reset);

            if (!result.IsSuccessful)
            {
                await context.Message.ReplyAsync("⚠️ Failed to set a character!").ConfigureAwait(false);
                return;
            }

            bool firstLaunch = !handler.Channels.Any();
            if (firstLaunch)
            {
                var savedData = GetStoredData(charId);

                handler.BlackList = savedData.BlackList;
                Log("Restored blocked users: ");
                Success(handler.BlackList.Count.ToString());

                handler.Channels = savedData.Channels;
                Log("Restored channels: ");
                Success(handler.Channels.Count.ToString());
            }
            else handler.Channels.Clear();                

            SaveData(channels: handler.Channels);

            string reply = cI.CurrentCharacter.Greeting!;

            if (BotConfig.CharacterAvatarEnabled)
                try { await SetBotNickname(cI.CurrentCharacter.Name!, context.Client).ConfigureAwait(false); }
                catch { reply += "\n⚠️ Failed to set bot name! Probably, missing permissions?"; }
            if (BotConfig.CharacterNameEnabled)
                try { await SetBotAvatar(context.Client.CurrentUser, cI.CurrentCharacter!).ConfigureAwait(false); }
                catch { reply += "\n⚠️ Failed to set bot avatar!"; }
            if (BotConfig.DescriptionInPlaying)
                _ = UpdatePlayingStatus(context.Client, integration: cI).ConfigureAwait(false);

            await context.Message
                .ReplyAsync($"{context.Message.Author.Mention} {reply}")
                .ConfigureAwait(false);
        }

        public static async Task FindCharacterAsync(string query, CommandsHandler handler, SocketCommandContext context)
        {
            var integration = handler.CurrentIntegration;
            var response = await integration.SearchAsync(query);

            if (response.IsEmpty)
            {
                await context.Message.ReplyAsync("⚠️ No characters were found").ConfigureAwait(false);
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

        public static async Task ResetCharacterAsync(CommandsHandler handler, SocketCommandContext context)
        {
            var newHistoryId = await handler.CurrentIntegration.CreateNewChatAsync();
            if (newHistoryId is null) return;

            var currentChannel = handler.Channels.Find(c => c.Id == context.Channel.Id);
            
            var newChannel = new Channel(context.Channel.Id, context.User.Id, newHistoryId, handler.CurrentIntegration.CurrentCharacter.Id!);

            if (currentChannel is not null)
            {
                newChannel.GuestsList = currentChannel.GuestsList;
                newChannel.Data.AudienceMode = currentChannel.Data.AudienceMode;
                handler.Channels.Remove(currentChannel);
            }

            handler.Channels.Add(newChannel);

            SaveData(handler.Channels);

            await context.Message.ReplyAsync(handler.CurrentIntegration.CurrentCharacter.Greeting!);
        }

        public static async Task CreatePrivateChatAsync(CommandsHandler handler, SocketCommandContext context)
        {
            var cI = handler.CurrentIntegration;
            var newChatHistoryId = await cI.CreateNewChatAsync();
            if (newChatHistoryId is null)
            {
                await context.Message.ReplyAsync("Failed to create new chat!");
                return;
            }

            ulong categoryId = await FindOrCreateCategoryAsync(context, BotConfig.Category);
            var newChannel = await CreateChannelAsync(context, categoryId, cI);
            var infoMsg = await newChannel.SendMessageAsync($"History Id: **`{newChatHistoryId}`**" +
                                                            $"Created by: {context.User.Id}" +
                                                            $"Use **`add {newChannel.Id} @user`** to add other users to this channel.");
            await infoMsg.PinAsync();
            await newChannel.SendMessageAsync(cI.CurrentCharacter.Greeting);

            bool isOwner = context.User.Id == context.Guild.OwnerId;
            bool isManager = isOwner || ((SocketGuildUser)context.User).Roles.Any(r => r.Name == BotConfig.BotRole);
            if (!isManager) // owner's and managers' channels will not be deactivated
                DeactivateOldUserChannel(handler, context.User.Id);

            // Update channels list
            var newChannelItem = new Channel(newChannel.Id, context.User.Id, newChatHistoryId, cI.CurrentCharacter.Id!);
            handler.Channels.Add(newChannelItem);

            SaveData(channels: handler.Channels);
        }

        private static async Task<ulong> FindOrCreateCategoryAsync(SocketCommandContext context, string categoryName)
        {
            var category = context.Guild.CategoryChannels.FirstOrDefault(c => c.Name == categoryName);
            if (category is not null) return category.Id;
            
            var newCategory = (await context.Guild.CreateCategoryChannelAsync(categoryName, c =>
            {
                var perms = new List<Overwrite>();
                var hideChannel = new OverwritePermissions(viewChannel: PermValue.Deny);
                perms.Add(new Overwrite(context.Guild.EveryoneRole.Id, PermissionTarget.Role, hideChannel));
                c.PermissionOverwrites = perms;
            }));

            return newCategory.Id;
        }

        private static async Task<RestTextChannel> CreateChannelAsync(SocketCommandContext context, ulong categoryId, Integration cI)
        {
            string channelName = $"private chat with {cI.CurrentCharacter.Name}";
            var hideChannel = new OverwritePermissions(viewChannel: PermValue.Deny);
            var showChannel = new OverwritePermissions(viewChannel: PermValue.Allow);

            var newChannel = await context.Guild.CreateTextChannelAsync(channelName, c =>
            {
                c.CategoryId = categoryId;
                c.PermissionOverwrites = new List<Overwrite>
                {
                    new Overwrite(context.Guild.EveryoneRole.Id, PermissionTarget.Role, hideChannel),
                    new Overwrite(context.Message.Author.Id, PermissionTarget.User, showChannel),
                    new Overwrite(context.Client.CurrentUser.Id, PermissionTarget.User, showChannel)
                };
            });

            return newChannel;
        }

        private static void DeactivateOldUserChannel(CommandsHandler handler, ulong userId)
        {
            var userChannels = handler.Channels.Where(c => c.AuthorId == userId);
            var newChannelsList = new List<Channel>();
            newChannelsList.AddRange(handler.Channels); // add all channels

            foreach (var uC in userChannels)
            {
                var delChannel = handler.Channels.Find(c => c.Id == uC.Id);
                if (delChannel is not null)
                    newChannelsList.Remove(delChannel); // delete old channel
            }
            handler.Channels = newChannelsList;
        }

        public static async Task SetBotNickname(string name, DiscordSocketClient client)
        {
            var guildID = client.Guilds.First().Id;
            var botAsGuildUser = client.GetGuild(guildID).GetUser(client.CurrentUser.Id);

            await botAsGuildUser.ModifyAsync(u => { u.Nickname = name; }).ConfigureAwait(false);
        }

        public static async Task SetBotAvatar(SocketSelfUser bot, Character character)
        {
            Stream image;
            byte[]? response = await TryDownloadImg(character.AvatarUrlFull!, 1);
            response ??= await TryDownloadImg(character.AvatarUrlMini!, 1);

            if (response is null)
            {
                Failure($"Failed to set bot avatar");
                Log("Setting default avatar... ");

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

                return;
            }

            image = new MemoryStream(response);
            await bot.ModifyAsync(u => { u.Avatar = new Discord.Image(image); }).ConfigureAwait(false);
        }

        public static async Task UpdatePlayingStatus(DiscordSocketClient client, int type = 0, string? status = null, Integration? integration = null)
        {
            if (integration is not null)
                status = integration.CurrentCharacter.IsEmpty ? "No character selected" : integration.CurrentCharacter.Title;
            else if (status == "0")
                status = null;
            
            await client.SetGameAsync(status, type: (ActivityType)type).ConfigureAwait(false);
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
            var requiredRole = roles.FirstOrDefault(role => role.Name == BotConfig.BotRole);

            return user.Roles.Contains(requiredRole);
        }
    }
}
