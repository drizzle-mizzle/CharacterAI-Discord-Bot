using CharacterAI_Discord_Bot.Service;

namespace CharacterAI_Discord_Bot.Models
{
    internal class DiscordChannel
    {
        internal ulong Id { get; set; }
        internal ulong AuthorId { get; set; }
        internal List<ulong> GuestsList { get; set; }
        internal CharacterDialogData Data { get; set; }
        internal DiscordChannel(ulong channelId, ulong authorId, CharacterDialogData data)
        {
            Id = channelId;
            AuthorId = authorId;
            GuestsList = new();
            Data = data;
        }
    }

    internal class CharacterDialogData : CommonService
    {
        internal string? HistoryId { get; set; }
        internal string? CharacterId { get; set; }
        internal int AudienceMode { get; set; } = BotConfig.DefaultAudienceMode;
        internal float ReplyChance { get; set; } = 0;
        internal int ReplyDelay { get; set; } = BotConfig.DefaultRepliesDealy;
        internal int SkipMessages { get; set; } = 0;
        internal bool SkipNextBotMessage { get; set; } = false;
        internal ulong LastCharacterCallMsgId { get; set; } = 0;// discord message id
        internal Dictionary<ulong, int> HuntedUsers { get; set; } = new(); // user id : reply chance
        internal LastCharacterCall? LastCall { get; set; }

        public CharacterDialogData(string? characterId, string? historyId)
        {
            HistoryId = historyId;
            CharacterId = characterId;
        }
    }
}
