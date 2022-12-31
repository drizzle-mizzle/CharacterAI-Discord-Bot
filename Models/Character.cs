using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterAI_Discord_Bot
{
    public class Character
    {
        public string? CharID { get; set; }
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? Greeting { get; set; }
        public string? Description { get; set; }
        public string? Tgt { get; set; }
        public string? AvatarUrl { get; set; }
        public string? HistoryExternalID { get; set; }
    }
}
