namespace CharacterAiDiscordBot.Models.Database
{
    public class HuntedUser
    {
        public int Id { get; set; }
        public required ulong UserId { get; set; }
        public required float Chance { get; set; }
        public required ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; } = null!;
    }
}
