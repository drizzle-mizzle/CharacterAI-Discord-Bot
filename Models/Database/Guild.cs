namespace CharacterAiDiscordBot.Models.Database
{
    public class Guild
    {
        public required ulong Id { get; set; }
        public string? GuildMessagesFormat { get; set; } = null;

        public virtual List<Channel> Channels { get; } = new();
        public virtual List<BlockedUser> BlockedUsers { get; } = new();
        public virtual List<HuntedUser> HuntedUsers { get; set; } = new();
    }
}
