namespace CharacterAI_Discord_Bot.Models
{
    public class LastSearch
    {
        public LastSearch()
            => SetDefaults();

        public List<dynamic>? characters;
        public int pages;
        public int currentRow;
        public int currentPage;
        public string? query;

        public void SetDefaults()
        {
            characters = null!;
            pages = 1;
            currentRow = 1;
            currentPage = 1;
            query = null!;
        }
    }
}
