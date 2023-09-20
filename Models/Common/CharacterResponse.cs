namespace CharacterAiDiscordBot.Models.Common
{
    public class CharacterResponse
    {
        public required string Text { get; set; }
        public required string? CharacterMessageId { get; set; }
        public required string? UserMessageId { get; set; }
        public required string? ImageRelPath { get; set; }
        public required bool IsSuccessful { get; set; }
        public bool IsFailure { get => !IsSuccessful; }
    }
}
