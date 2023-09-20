using Discord.Interactions;
using CharacterAiDiscordBot.Services;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.CommandsService;
using static CharacterAiDiscordBot.Services.StorageContext;
using static CharacterAiDiscordBot.Services.IntegrationService;
using Discord;
using CharacterAiDiscordBot.Models.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterAiDiscordBot.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    [Group("show", "Show-commands")]
    public class ShowCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationService _integration;

        public ShowCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationService>();
        }

        [SlashCommand("cai-history-id", "Show c.ai history ID")]
        public async Task ShowCaiHistoryId()
        {
            await ShowHistoryIdAsync();
        }

        [SlashCommand("messages-format", "Check default or character messages format")]
        public async Task ShowMessagesFormat()
        {
            await ShowMessagesFormatAsync();
        }

        [SlashCommand("character-info", "Info")]
        public async Task ShowCharacterInfo()
        {
            await RespondAsync(embed: CharacterInfoEmbed(_integration.SelfCharacter!));
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task ShowHistoryIdAsync()
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Current history ID: `{channel.HistoryId ?? "not set"}`".ToInlineEmbed(Color.Green));
        }
        
        private async Task ShowMessagesFormatAsync()
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);

            string format = channel.ChannelMessagesFormat ?? channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            string text = format.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer");

            if (text.Contains("{{ref_msg_text}}"))
            {
                text = text.Replace("{{ref_msg_text}}", "Hola")
                           .Replace("{{ref_msg_begin}}", "")
                           .Replace("{{ref_msg_end}}", "")
                           .Replace("{{ref_msg_user}}", "Dude")
                           .Replace("\\n", "\n");
            }

            var embed = new EmbedBuilder().WithTitle("Default messages format")
                                          .WithColor(Color.Gold)
                                          .AddField("Format:", $"`{format}`")
                                          .AddField("Example", $"Referenced message: *`Hola`* from user *`Dude`*\n" +
                                                               $"User nickname: `Average AI Enjoyer`\n" +
                                                               $"User message: *`Hello!`*\n" +
                                                               $"Result (what character will see):\n*`{text}`*");

            await FollowupAsync(embed: embed.Build());
        }
    }
}
