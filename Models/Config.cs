using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterAI_Discord_Bot.Models
{
    internal class Config
    {
        public string UserToken { get; }
        public string BotToken { get; }
        public string BotRole { get; }
        public string[] BotPrefixes { get; }
        public int DefaultAudienceMode { get; }
        public string Nopower { get; }
        public bool PrivateChatRoleRequired { get; set; }
        public bool DescriptionInPlaying { get; set; }
        //public bool SeparateHistoryOnlyInPrivates { get; set; }
        public int RateLimit { get; }
        public int RemoveDelay { get; }
        public bool AutoSetupEnabled { get; }
        public string AutoCharId { get; }

        public Config(StreamReader configJson)
        {
            var configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd())!;

            UserToken = configParsed["char_ai_user_token"]!.Value<string>()!;
            BotToken = configParsed["discord_bot_token"]!.Value<string>()!;
            BotRole = configParsed["discord_bot_role"]!.Value<string>()!;
            BotPrefixes = JsonConvert.DeserializeObject<string[]>(configParsed["discord_bot_prefixes"]!.ToString())!;
            DefaultAudienceMode = configParsed["default_audience_mode"]!.Value<int>()!;
            Nopower = configParsed["default_no_permission_file"]!.Value<string>()!;
            PrivateChatRoleRequired = bool.Parse(configParsed["private_chat_role_required"]!.Value<string>()!);
            DescriptionInPlaying = bool.Parse(configParsed["description_in_playing_status"]!.Value<string>()!);
            //SeparateHistoryOnlyInPrivates = bool.Parse(configParsed["separate_chat_history_only_for_privates"]!.Value<string>()!);
            RateLimit = configParsed["rate_limit"]!.Value<int>()!;
            RemoveDelay = configParsed["buttons_remove_delay"]!.Value<int>()!;
            AutoSetupEnabled = bool.Parse(configParsed["auto_setup"]!.Value<string>()!)!;
            AutoCharId = configParsed["auto_char_id"]!.Value<string>()!;
        }

    }
}
