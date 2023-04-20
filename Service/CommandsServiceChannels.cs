using CharacterAI;
using Discord.Commands;
using Discord.Rest;
using Discord;
using Discord.WebSocket;
using CharacterAI_Discord_Bot.Handlers;
using CharacterAI_Discord_Bot.Models;

namespace CharacterAI_Discord_Bot.Service
{
    /// <summary>
    /// Separate partial class file for a static methods working with channels.
    /// </summary>
    public partial class CommandsService : CommonService
    {
        internal static void DeactivateOldUserChannel(CommandsHandler handler, ulong userId)
        {
            var userChannels = handler.Channels.Where(c => c.AuthorId == userId);
            var newChannelsList = new List<DiscordChannel>();
            newChannelsList.AddRange(handler.Channels); // add all channels

            foreach (var userChannel in userChannels)
            {
                var channelToRemove = handler.Channels.Find(c => c.Id == userChannel.Id);
                if (channelToRemove is null) continue;

                newChannelsList.Remove(channelToRemove); // delete old channel
            }

            // Update channels list with a new list without old channels
            handler.Channels = newChannelsList;
        }

        internal static async Task<ulong> FindOrCreateCategoryAsync(SocketCommandContext context, string categoryName)
        {
            // Find category on server by its name.
            var category = context.Guild.CategoryChannels.FirstOrDefault(c => c.Name == categoryName);
            if (category is not null) return category.Id;

            // Create category if it does not exist.
            var newCategory = await context.Guild.CreateCategoryChannelAsync(categoryName, c =>
            {   // Hide it for everyone except bot
                c.PermissionOverwrites = new List<Overwrite>()
                {
                    ViewChannelPermOverwrite(context.Client.CurrentUser, PermValue.Allow),
                    ViewChannelPermOverwrite(context.Guild.EveryoneRole, PermValue.Deny)
                };
            });

            return newCategory.Id;
        }

        internal static async Task<RestTextChannel> CreateChannelAsync(SocketCommandContext context, ulong categoryId, Integration cI)
        {
            string channelName = $"private chat with {cI.CurrentCharacter.Name}";
            var category = context.Guild.GetCategoryChannel(categoryId);

            var catPerms = new List<Overwrite>() { ViewChannelPermOverwrite(context.Message.Author, PermValue.Allow) };
            await category.ModifyAsync(c => c.PermissionOverwrites = catPerms);

            var channelPerms = new List<Overwrite>()
            {
                ViewChannelPermOverwrite(context.Message.Author, PermValue.Allow),
                ViewChannelPermOverwrite(context.Client.CurrentUser, PermValue.Allow),
                ViewChannelPermOverwrite(context.Guild.EveryoneRole, PermValue.Deny)
            };
            var newChannel = await context.Guild.CreateTextChannelAsync(channelName, c
                => { c.CategoryId = categoryId; c.PermissionOverwrites = channelPerms; });

            return newChannel;
        }

        public static Overwrite ViewChannelPermOverwrite(ISnowflakeEntity target, PermValue permValue)
        {
            PermissionTarget permTarget = target is SocketUser ? PermissionTarget.User : PermissionTarget.Role;
            var permission = new OverwritePermissions(viewChannel: permValue);

            return new Overwrite(target.Id, permTarget, permission);
        }
    }
}
