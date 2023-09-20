using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterAiDiscordBot.Services
{
    public class RequireManagerAccess : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
        {
            if (context.User is not SocketGuildUser guildUser)
                return PreconditionResult.FromError("Context is not a guild");

            if (guildUser.HasManagerRole() || guildUser.IsServerOwner() || guildUser.IsHoster())
                return PreconditionResult.FromSuccess();
            else
            {
                await context.SendNoPowerFileAsync();
                return PreconditionResult.FromError("Not a manager");
            }
        }
    }

    public class RequireHosterAccess : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
        {
            if (context.User is not SocketGuildUser guildUser)
                return PreconditionResult.FromError("Context is not a guild");

            if (guildUser.IsHoster())
                return PreconditionResult.FromSuccess();
            else
            {
                await context.SendNoPowerFileAsync();
                return PreconditionResult.FromError("This command is only available to the bot owner");
            }
        }
    }
}
