using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.IntegrationService;
using static CharacterAiDiscordBot.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterAiDiscordBot.Services;

namespace CharacterAiDiscordBot.Handlers
{
    public class SlashCommandsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;

        public SlashCommandsHandler(IServiceProvider services)
        {
            _services = services;
            _interactions = _services.GetRequiredService<InteractionService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.SlashCommandExecuted += (command) =>
            {
                Task.Run(async () =>
                {
                    try { await HandleCommandAsync(command); }
                    catch (Exception e) { HandleSlashCommandException(command, e); }
                });
                return Task.CompletedTask;
            };

            _interactions.InteractionExecuted += (info, context, result) =>
            {
                if (!result.IsSuccess)
                    Task.Run(async () => await HandleInteractionExceptionAsync(context, result));

                return Task.CompletedTask;
            };
        }

        private async Task HandleCommandAsync(SocketSlashCommand command)
        {
            if (await UserIsBannedCheckOnly(command.User.Id)) return;

            var context = new InteractionContext(_client, command, command.Channel);
            await _interactions.ExecuteCommandAsync(context, _services);
        }


        private async Task HandleInteractionExceptionAsync(IInteractionContext context, IResult result)
        {
            LogRed(result.ErrorReason + "\n");
            string message = result.ErrorReason;

            try { await context.Interaction.RespondAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{message}`".ToInlineEmbed(Color.Red)); }
            catch { await context.Interaction.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{message}`".ToInlineEmbed(Color.Red)); }

            var channel = context.Channel;
            var guild = context.Guild;

            // don't need that shit in logs
            bool ignore = result.Error.GetValueOrDefault().ToString().Contains("UnmetPrecondition") || result.ErrorReason.Contains("was not in a correct format");
            if (ignore) return;

            var originalResponse = await context.Interaction.GetOriginalResponseAsync();
            var owner = guild is null ? null : (await guild.GetOwnerAsync()) as SocketGuildUser;

            TryToReportInLogsChannel(_client, title: "Slash Command Exception",
                                              desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{owner?.GetBestName()} ({owner?.Username})`\n" +
                                                    $"Channel: `{channel.Name} ({channel.Id})`\n" +
                                                    $"User: `{context.User.Username}`\n" +
                                                    $"Slash command: `{originalResponse.Interaction.Name}`",
                                              content: $"{message}\n\n{result.Error.GetValueOrDefault()}",
                                              color: Color.Red,
                                              error: true);
        }

        private void HandleSlashCommandException(SocketSlashCommand command, Exception e)
        {
            LogException(new[] { e });
            var channel = command.Channel as SocketGuildChannel;
            var guild = channel?.Guild;

            List<string> commandParams = new();
            foreach (var option in command.Data.Options)
            {
                var val = option.Value.ToString() ?? "";
                int l = Math.Min(val.Length, 20);
                commandParams.Add($"{option.Name}:{val[0..l] + (val.Length > 20 ? "..." : "")}");
            }
            
            TryToReportInLogsChannel(_client, title: "Slash Command Exception",
                                              desc: $"In Guild `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                    $"Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                    $"User: `{command.User?.Username}`\n" +
                                                    $"Slash command: `/{command.CommandName}` `[{string.Join(" | ", commandParams)}]`",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }
    }
}
