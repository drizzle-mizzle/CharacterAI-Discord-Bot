using Newtonsoft.Json.Linq;
using CharacterAiDiscordBot.Services;
using System.Data.SqlTypes;

namespace CharacterAiDiscordBot.Models.Common
{
    public static class ConfigFile
    {
        public static ConfigField DiscordBotToken { get; } = new("discord_bot_token");
        public static ConfigField DiscordBotRole { get; } = new("discord_bot_manager_role");
        
        public static ConfigField HosterDiscordID { get; } = new("hoster_discord_id");
        public static ConfigField DiscordLogsChannelID { get; } = new("discord_logs_channel_id");
        public static ConfigField DiscordErrorLogsChannelID { get; } = new("discord_error_logs_channel_id");
     
        public static ConfigField DefaultMessagesFormat { get; } = new("default_messages_format");
        public static ConfigField NoPermissionFile { get; } = new("no_permission_file");
        public static ConfigField RateLimit { get; } = new("rate_limit");

        public static ConfigField PuppeteerBrowserDir { get; } = new("puppeteer_browser_directory");
        public static ConfigField PuppeteerBrowserExe { get; } = new("puppeteer_browser_executable_path");
        
        private static JObject ConfigParsed { get; } = CommonService.TryToParseConfigFile();

        public class ConfigField
        {
            public readonly string Label;
            public string? Value {
                get
                {
                    string? data = ConfigParsed[Label]?.Value<string?>();
                    return string.IsNullOrWhiteSpace(data) ? null : data;
                }
            }

            public ConfigField(string label)
            {
                Label = label;
            }
        }
    }
}
