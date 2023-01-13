namespace CharacterAI_Discord_Bot.Models
{
    public class LastResponse
    {
        public dynamic replies = null!;
        public int currReply = 0;
        public string primaryMsgId = null!;
        public string lastUserMsgId = null!;

        public void SetDefaults()
        {
            replies = null!;
            currReply = 0;
            primaryMsgId = null!;
            lastUserMsgId = null!;
        }
    }
}
