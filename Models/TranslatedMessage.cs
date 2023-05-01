using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CharacterAI_Discord_Bot.Models
{
    internal class TranslatedMessage
    {
        internal ulong MessageId { get; set; }
        internal List<string> OriginalTexts { get; set; } = new();
        internal Dictionary<int, string> TranslatedTexts { get; set; } = new();
        internal int LastTextIndex { get; set; }

        /// <summary>
        /// The message "state"; false when original message is displayed; true when translated text is displayed.
        /// </summary>
        internal bool IsTranslated { get; set; } = false;

        internal TranslatedMessage(IMessage message)
        {
            MessageId = message.Id;
            OriginalTexts.Add(message.Content);
        }
    }
}
