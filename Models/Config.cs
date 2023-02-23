using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CharacterAI_Discord_Bot.Models
{
    internal class Config
    {
        public bool AutoSetupEnabled { get; }
        public string AutoCharId { get; }
        public string BotToken { get; }
        public string BotRole { get; }
        public string[] BotPrefixes { get; }
        public string Category { get; }
        public bool CharacterAvatarEnabled { get; }
        public bool CharacterNameEnabled { get; }
        public int DefaultAudienceMode { get; }
        public bool DescriptionInPlaying { get; }
        public string Nopower { get; }
        public bool DMenabled { get; }
        public bool PrivateChatRoleRequired { get; }
        public int RateLimit { get; }
        public int RemoveDelay { get; }
        public bool SwipesEnabled { get; }
        public string UserToken { get; }

        //public bool SeparateHistoryOnlyInPrivates { get; set; }

        private readonly JObject _configParsed;

        public Config(StreamReader configJson)
        {
            _configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd())!;

            AutoSetupEnabled = bool.Parse(GetValue("auto_setup"))!;
            AutoCharId = GetValue("auto_char_id");
            BotPrefixes = JsonConvert.DeserializeObject<string[]>(_configParsed["discord_bot_prefixes"]!.ToString())!;
            BotRole = GetValue("discord_bot_role");
            BotToken = GetValue("discord_bot_token");
            Category = GetValue("discord_private_category_name");
            CharacterAvatarEnabled = bool.Parse(GetValue("use_character_avatar"));
            CharacterNameEnabled = bool.Parse(GetValue("use_character_name"));
            DefaultAudienceMode = int.Parse(GetValue("default_audience_mode"));
            DescriptionInPlaying = bool.Parse(GetValue("description_in_playing_status"));
            DMenabled = bool.Parse(GetValue("allow_dm"));
            Nopower = GetValue("default_no_permission_file");
            PrivateChatRoleRequired = bool.Parse(GetValue("private_chat_role_required"));
            RateLimit = int.Parse(GetValue("rate_limit"));
            RemoveDelay = int.Parse(GetValue("buttons_remove_delay"));
            SwipesEnabled = bool.Parse(GetValue("enable_swipe_buttons"));
            UserToken = GetValue("char_ai_user_token");
            //SeparateHistoryOnlyInPrivates = bool.Parse(configParsed["separate_chat_history_only_for_privates"]!.Value<string>()!);
        }

        private string GetValue(string key)
            => _configParsed[key]!.Value<string>()!;

    }
}
