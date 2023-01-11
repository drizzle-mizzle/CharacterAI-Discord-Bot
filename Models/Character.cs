using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterAI_Discord_Bot
{
    public class Character
    {
        public string? CharId { get; set; }
        public string? Name { get; set; }
        public string? Greeting { get; set; }
        public string? Tgt { get; set; }
        public string? AvatarUrl { get; set; }
        public string? HistoryExternalId { get; set; }

        private string? title;
        private string? description;

        public string Title
        {
            get => title!;
            set => title = value.Trim(' ');
        }
        public string Description
        {
            get => description!;
            set => description = value.Trim(' ');
        }
    }
}
