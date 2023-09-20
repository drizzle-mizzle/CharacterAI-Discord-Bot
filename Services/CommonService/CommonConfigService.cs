using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CharacterAiDiscordBot.Services
{
    internal static partial class CommonService
    {
        internal static JObject TryToParseConfigFile()
        {
            try
            {

                using StreamReader sr = File.Exists($"{EXE_DIR}{SC}env.config.json") ?
                                                new($"{EXE_DIR}{SC}env.config.json") :
                                                new($"{EXE_DIR}{SC}config.json");

                string content = sr.ReadToEnd();
                var file = (JObject)JsonConvert.DeserializeObject(content)!;

                return file;
            }
            catch (Exception e)
            {
                LogRed("\nSomething went wrong...\nCheck your ");
                LogYellow("config.json ");
                LogRed($"file. (probably, missing some comma or quotation mark?)\n\nDetails:\n{e}");

                throw;
            }
        }
    }
}
