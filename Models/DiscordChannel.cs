using CharacterAI_Discord_Bot.Service;

namespace CharacterAI_Discord_Bot.Models
{
    internal class DiscordChannel
    {
        internal ulong ChannelId { get; set; }
        internal string ChannelName { get; set; } = "";
        internal ulong ChannelAuthorId { get; set; }
        internal ulong GuildId { get; set; } = 0;
        internal string GuildName { get; set; } = "";
        internal ChannelData Data { get; set; }
        internal DiscordChannel(ulong channelId, ulong authorId, ChannelData data)
        {
            ChannelId = channelId;
            ChannelAuthorId = authorId;
            Data = data;
        }
    }

    internal class ChannelData : CommonService
    {
        internal string? HistoryId { get; set; }
        internal string? CharacterId { get; set; }
        internal int AudienceMode { get; set; } = BotConfig.DefaultAudienceMode;
        internal float ReplyChance { get; set; } = 0;
        internal int ReplyDelay { get; set; } = BotConfig.DefaultRepliesDealy;
        internal int SkipMessages { get; set; } = 0;
        internal bool SkipNextBotMessage { get; set; } = false;
        internal string TranslateLanguage { get; set; } = BotConfig.DefaultTranslateLanguage;
        internal ulong LastCharacterCallMsgId { get; set; } = 0;// discord message id
        internal Dictionary<ulong, int> HuntedUsers { get; set; } = new(); // user id : reply chance
        internal List<ulong> GuestsList { get; set; } = new();
        internal LastCharacterCall? LastCall { get; set; }

        public ChannelData(string? characterId, string? historyId)
        {
            HistoryId = historyId;
            CharacterId = characterId;
        }
    }
}
