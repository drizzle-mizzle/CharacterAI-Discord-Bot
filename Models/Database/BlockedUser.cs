namespace CharacterAiDiscordBot.Models.Database
{
    public class BlockedUser
    {
        public required ulong Id { get; set; }
        public required DateTime From { get; set; }
        public required int Hours { get; set; }
        public ulong? GuildId { get; set; } = null;
        public virtual Guild? Guild { get; set; }
    }
}
