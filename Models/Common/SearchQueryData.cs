using CharacterAiDiscordBot.Models.Common;
using static CharacterAiDiscordBot.Services.IntegrationService;

namespace CharacterAiDiscordBot.Models.Common
{
    public class SearchQueryData
    {
        public List<Character> Characters { get; }
        public string OriginalQuery { get; }
        public string? ErrorReason { get; set; }
        public bool IsSuccessful => ErrorReason == null;
        public bool IsEmpty => !Characters.Any();

        public SearchQueryData(List<Character> characters, string query)
        {
            Characters = characters;
            OriginalQuery = query;
        }
    }
}
