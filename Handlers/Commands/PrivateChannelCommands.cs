using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static CharacterAI_Discord_Bot.Service.CommandsService;
using static CharacterAI_Discord_Bot.Service.CommonService;

namespace CharacterAI_Discord_Bot.Handlers.Commands
{
    public class PrivateChannelCommands : ModuleBase<SocketCommandContext>
    {
        private readonly CommandsHandler _handler;
        private Models.DiscordChannel? GetCurrentChannel(ulong channelId)
            => _handler.Channels.Find(c => c.Id == channelId && c.AuthorId == Context.User.Id);

        public PrivateChannelCommands(CommandsHandler handler)
            => _handler = handler;

        [Command("private")]
        [Alias("pc")]
        public async Task CreatePrivateChat()
        {
            if (Context.Guild.CurrentUser.GuildPermissions.ManageChannels is false)
                await Context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} `ManageChannels` permission was not provided.");
            else if (BotConfig.PrivateChatRoleRequired && !ValidateUserAccess(Context))
                await NoPermissionAlert(Context).ConfigureAwait(false);
            else if (_handler.CurrentIntegration.CurrentCharacter.IsEmpty)
                await Context.Message.ReplyAsync($"{WARN_SIGN_DISCORD} Set a character first").ConfigureAwait(false);
            else
                _ = CreatePrivateChatAsync(_handler, Context);
        }

        [Command("add")]
        public async Task AddUser(ulong channelId, SocketGuildUser user)
        {
            var currentChannel = GetCurrentChannel(channelId);
            if (currentChannel is null)
            {
                await Context.Message.ReplyAsync("Either you are not the creator of this chat, or it was deactivated.");
                return;
            }

            var showChannel = new OverwritePermissions(viewChannel: PermValue.Allow);
            var discordChannel = Context.Guild.GetChannel(channelId);
            await discordChannel.AddPermissionOverwriteAsync(user, showChannel);

            currentChannel.GuestsList.Add(user.Id);

            SaveData(channels: _handler.Channels);
        }

        [Command("kick")]
        public async Task KickUser(SocketGuildUser user)
        {
            var currentChannel = GetCurrentChannel(Context.Channel.Id);
            if (currentChannel is null)
            {
                await Context.Message.ReplyAsync("Either you are not the creator of this chat, or it was deactivated.");
                return;
            }

            var hideChannel = new OverwritePermissions(viewChannel: PermValue.Deny);
            var discordChannel = Context.Guild.GetChannel(Context.Channel.Id);
            await discordChannel.AddPermissionOverwriteAsync(user, hideChannel);

            currentChannel.GuestsList.Remove(user.Id);

            SaveData(channels: _handler.Channels);
        }

        [Command("clear")]
        public async Task ClearPrivates()
        {
            if (!ValidateUserAccess(Context))
            {
                await NoPermissionAlert(Context).ConfigureAwait(false);
                return;
            }

            var category = Context.Guild.CategoryChannels.FirstOrDefault(c => c.Name == BotConfig.Category);
            if (category is null) return;

            foreach (var channel in category.Channels.Cast<SocketTextChannel>())
            {
                if (!channel.Name.Contains("private-chat-with")) continue;
                if (_handler.Channels.Find(c => c.Id == channel.Id) is null)
                    _ = channel.DeleteAsync();
            }
        }
    }
}
