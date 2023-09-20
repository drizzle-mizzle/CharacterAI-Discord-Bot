using CharacterAiDiscordBot.Models.Database;

namespace CharacterAiDiscordBot.Models.Common
{
    public class Character
    {
        public required string Id { get; set; }
        public required string Tgt { get; set; }
        public required string Name { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required string Greeting { get; set; }
        public required string AuthorName { get; set; }
        public required bool ImageGenEnabled { get; set; }
        public string? AvatarUrl { get; set; }
        public required ulong Interactions { get; set; }
    }
}
