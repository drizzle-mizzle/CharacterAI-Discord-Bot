using System.Dynamic;
using CharacterAI_Discord_Bot.Models;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class IntegrationService : CommonService
    {
        public static bool HelloLog(Character charInfo)
        {
            Log("\nCharacterAI - Connected\n\n", ConsoleColor.Green);
            Log($" [{charInfo.Name}]\n\n", ConsoleColor.Cyan);
            Log($"{charInfo.Greeting}\n");
            if (!string.IsNullOrEmpty(charInfo.Description))
                Log($"\"{charInfo.Description}\"\n");
            Log("\nSetup complete\n", ConsoleColor.Yellow);

            return Success(new string('<', 50) + "\n");
        }
        
        public static dynamic BasicCallContent(Character charInfo, string msg, string imgPath)
        {
            dynamic content = new ExpandoObject();

            if (!string.IsNullOrEmpty(imgPath))
            {
                content.image_description_type = "AUTO_IMAGE_CAPTIONING";
                content.image_origin_type = "UPLOADED";
                content.image_rel_path = imgPath;
            }

            content.character_external_id = charInfo.CharId!;
            content.enable_tti = true;
            content.history_external_id = charInfo.HistoryExternalId!;
            content.text = msg;
            content.tgt = charInfo.Tgt!;
            content.ranking_method = "random";
            content.staging = false;
            content.stream_every_n_steps = 16;
            content.chunks_to_pad = 8;
            content.is_proactive = false;

            return content;
        }
    }
}
