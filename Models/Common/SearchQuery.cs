using static CharacterAiDiscordBot.Services.IntegrationService;

namespace CharacterAiDiscordBot.Models.Common
{
    public class SearchQuery
    {
        public ulong AuthorId { get; }
        public ulong InteractionId { get; }
        public int Pages { get; }
        public int CurrentRow { get; set; }
        public int CurrentPage { get; set; }
        public SearchQueryData SearchQueryData { get; }
       
        public SearchQuery(ulong interactionId, ulong authorId, SearchQueryData data, int pages)
        {
            InteractionId = interactionId;
            AuthorId = authorId;
            SearchQueryData = data;
            Pages = pages;
            CurrentRow = 1;
            CurrentPage = 1;
        }
    }
}
