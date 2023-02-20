using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CharacterAI.Models;
using CharacterAI_Discord_Bot.Service;

namespace CharacterAI_Discord_Bot.Models
{
    internal class Channel
    {
        internal ulong Id { get; set; }
        internal ulong AuthorId { get; set; }
        internal List<ulong> GuestsList { get; set; }
        internal CharacterDialogData Data { get; set; }
        internal Channel(ulong channelId, ulong authorId, string historyId, string characterId)
        {
            Id = channelId;
            AuthorId = authorId;
            GuestsList = new();
            Data = new(historyId, characterId);
        }
    }

    internal class CharacterDialogData : CommonService
    {
        internal string HistoryId { get; }
        internal string CharacterId { get; }
        internal int AudienceMode { get; set; }
        internal ulong LastCharacterCallMsgId { get; set; } // discord message id
        internal int SkipMessages { get; set; }
        internal LastCharacterCall? LastCall { get; set; }

        public CharacterDialogData(string historyId, string characterId)
        {
            HistoryId = historyId;
            CharacterId = characterId;
            AudienceMode = BotConfig.DefaultAudienceMode;
            SkipMessages = 0;
            LastCharacterCallMsgId = 0;
        }
    }
}
