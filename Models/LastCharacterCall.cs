using CharacterAI.Models;

namespace CharacterAI_Discord_Bot.Models
{
    internal class LastCharacterCall
    {
        public LastCharacterCall(CharacterResponse cR)
        {
            OriginalResponse = cR;
            CurrentReplyIndex = 0;
            if (cR.Response is not null) RepliesList.Add(cR.Response);
        }

        public CharacterResponse OriginalResponse { get; set; }
        public List<Reply> RepliesList { get; set; } = new();
        public ulong? CurrentPrimaryMsgId { get; set; }
        public int CurrentReplyIndex { get; set; }
    }
}
