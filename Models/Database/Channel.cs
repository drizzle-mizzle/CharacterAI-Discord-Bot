namespace CharacterAiDiscordBot.Models.Database
{
    public class Channel
    {
        public ulong Id { get; set; }
        public string? HistoryId { get; set; }
        public string? ChannelMessagesFormat { get; set; } = null;
        public required int ResponseDelay { get; set; }
        public required float RandomReplyChance { get; set; }
        public required bool SwipesEnabled { get; set; }
        public required bool StopBtnEnabled { get; set; }
        public required bool SkipNextBotMessage { get; set; }
        public required int CurrentSwipeIndex { get; set; }

        /// <summary>
        /// The last user who have called a character
        /// </summary>
        public ulong LastDiscordUserCallerId { get; set; } = 0;

        /// <summary>
        /// To check if swipe buttons on the message should be handled (only the last one is active)
        /// </summary>
        public ulong LastCharacterDiscordMsgId { get; set; } = 0;

        /// <summary>
        /// To be put in the new swipe fetching request (parentMessageId)
        /// </summary>
        public string? LastUserMsgId { get; set; }

        /// <summary>
        /// To be put in the new response fetching request after swipe (primaryMessageId)
        /// </summary>
        public string? LastCharacterMsgId { get; set; }

        public required ulong MessagesSent { get; set; }
        public required DateTime LastCallTime { get; set; }

        public required ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; } = null!;
    }
}
