using Discord;
using Discord.Interactions;
using CharacterAiDiscordBot.Services;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.IntegrationService;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using CharacterAiDiscordBot.Models.Common;

namespace CharacterAiDiscordBot.Handlers.SlashCommands
{
    public class OtherCommands : InteractionModuleBase<InteractionContext>
    {
        //private readonly IntegrationsService _integration;
        private readonly DiscordSocketClient _client;
        //private readonly StorageContext _db;

        public OtherCommands(IServiceProvider services)
        {
            //_integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            //_db = new StorageContext();
        }


        [SlashCommand("help", "All basic info about bot")]
        public async Task BaicsHelp()
        {
            var embed = new EmbedBuilder().WithTitle("Character Engine").WithColor(Color.Gold)
                                          .WithDescription($"Only server owner can execute bot commands by default. If you want allow other users to manage bot settings, give them **{ConfigFile.DiscordBotRole.Value}** role. More settings can be found in `Server settings -> Integrations`.")
                                          .AddField("How to use", "1. Use one of the `/spawn` commands to create a character.\n" +
                                                                  "2. Modify it with one of the `/update` commands using a given prefix or webhook ID.\n" +
                                                                  "3. Call character by mentioning its prefix or with reply on one of its messages.\n" +
                                                                  "4. If you want to start the chat with a character from the beginning, use `/reset-character` command.\n" +
                                                                  "5. Read [wiki/Important-Notes-and-Additional-Guides](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Important-Notes-and-Additional-Guides) and [wiki/Commands](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Commands) to know more.")
                                          .AddField("API", "By default, bot will use its owner's credentials (if those are present) for accessing all needed servcies like **CharacterAI** or **OpenAI**\n" +
                                                           "To use your own API keys and cAI accounts, change it with `/set-server-[ type ]-token` command.\n" +
                                                           "Each character can use different credentials.")
                                          .AddField("Also", "It's really recommended to look into `/help-messages-format`");
                                          
            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("help-messages-format", "Info about messages format")]
        public async Task MessagesFormatHelp()
        {
            var embed = new EmbedBuilder().WithTitle("Messages format").WithColor(Color.Gold)
                                          .AddField("Description", "This setting allows you to change the format of messages that character will get from users.")
                                          .AddField("Commands", "`/show messages-format` - Check the current format of messages for this server or certain character\n" +
                                                                "`/update messages-format` - Change the format of messages for certain character\n" +
                                                                "`/set-server-messages-format` - Change the format of messages for all **new** characters on this server")
                                          .AddField("Placeholders", "You can use these placeholders in your formats to manipulate the data that being inserted in your messages:\n" +
                                                                    "**`{{msg}}`** - **Required** placeholder that contains the message itself.\n" +
                                                                    "**`{{user}}`** - Placeholder that contains the user's Discord name *(server nickname > display name > username)*.\n" +
                                                                    "**`{{ref_msg_begin}}`**, **`{{ref_msg_user}}`**, **`{{ref_msg_text}}`**, **`{{ref_msg_end}}`** - Combined placeholder that contains the referenced message (one that user was replying to). *Begin* and *end* parts are needed because user message can have no referenced message, and then placeholder will be removed.\n")
                                          .AddField("Example", "Format:\n*`{{ref_msg_begin}}((In response to '{{ref_msg_text}}' from '{{ref_msg_user}}')){{ref_msg_end}}\\n{{user}} says:\\n{{msg}}`*\n" +
                                                               "Inputs:\n- referenced message with text *`Hello`* from user *`Dude`*;\n- user with name *`Average AI Enjoyer`*;\n- message with text *`Do you love donuts?`*\n" +
                                                               "Result (what character will see):\n*`((In response to 'Hello' from 'Dude'))\nAverage AI Enjoyer says:\nDo you love donuts?`*\n" +
                                                               "Example above is used by default, but you are free to play with it the way you want, or you can simply disable it by setting the default message format with `{{msg}}`.");
            await RespondAsync(embed: embed.Build());
        }


        [SlashCommand("ping", "ping")]
        public async Task Ping()
        {
            await RespondAsync(embed: $":ping_pong: Pong! - {_client.Latency} ms".ToInlineEmbed(Color.Red));
        }
    }
}
