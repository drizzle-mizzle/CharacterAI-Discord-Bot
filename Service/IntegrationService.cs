using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class IntegrationService : CommonService
    {
        public static void SetupCompleteLog(Character charInfo)
        {
            Log("\nCharacterAI - Connected\n\n", ConsoleColor.Green);
            Log($" [{charInfo.Name}]\n\n", ConsoleColor.Cyan);
            Log($"{charInfo.Greeting}\n");
            if (!string.IsNullOrEmpty(charInfo.Description))
                Log($"\"{charInfo.Description}\"\n");
            Log("\nSetup complete\n", ConsoleColor.Yellow);
        }
        
        public static Dictionary<string, string> BasicCallContent(Character charInfo, string msg, string imgPath)
        {
            var content = new Dictionary<string, string>
            {
                { "character_external_id", charInfo.CharID! },
                { "enable_tti", "true" },
                { "history_external_id", charInfo.HistoryExternalID! },
                { "text", msg },
                { "tgt", charInfo.Tgt! },
                { "ranking_method", "random" },
                { "staging", "false" },
                { "stream_every_n_steps", "16" },
                { "chunks_to_pad", "8" },
                { "is_proactive", "false" },

            };

            if (!string.IsNullOrEmpty(imgPath))
            {
                content.Add("image_description_type", "AUTO_IMAGE_CAPTIONING");
                content.Add("image_origin_type", "UPLOADED");
                content.Add("image_rel_path", "imgPath");
            }

            return content;
        }
    }
}
