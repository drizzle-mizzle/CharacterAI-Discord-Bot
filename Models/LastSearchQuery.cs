using CharacterAI.Models;

namespace CharacterAI_Discord_Bot.Models
{
    public class LastSearchQuery
    {
        public LastSearchQuery(SearchResponse sR)
        {
            Response = sR;
            Pages = 1;
            CurrentRow = 1;
            CurrentPage = 1;
            Query = null;
        }

        public SearchResponse Response { get; set; }
        public int Pages { get; set; }
        public int CurrentRow { get; set; }
        public int CurrentPage { get; set; }
        public string? Query { get; set; }

    }
}
