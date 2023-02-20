using CharacterAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterAI_Discord_Bot.Models
{
    internal class LastCharacterCall
    {
        public LastCharacterCall(CharacterResponse cR)
        {
            OriginalResponse = cR;
            RepliesList = cR.Replies;
            CurrentReplyIndex = 0;
        }

        public CharacterResponse OriginalResponse { get; set; }
        public List<Reply> RepliesList { get; set; }
        public ulong? CurrentPrimaryMsgId { get; set; }
        public int CurrentReplyIndex { get; set; }
    }
}
