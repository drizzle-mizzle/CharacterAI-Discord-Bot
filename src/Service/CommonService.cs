using Discord;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterAI_Discord_Bot.Service
{
    public class CommonService
    {

        public string botImgPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "characterAvatar.avif";
        public static dynamic GetConfig()
        {
            using StreamReader configJson = new StreamReader(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Config.json");
            var configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
            dynamic config = new
            {
                userToken = configParsed["char_ai_user_token"].Value<string>(),
                botToken = configParsed["discord_bot_token"].Value<string>()
            };

            return config;
        }

        public static bool Success(string logText = "")
        {
            Log(logText, ConsoleColor.Green);

            return true;
        }

        public static bool Failure(string logText = "")
        {
            Log(logText, ConsoleColor.Red);

            return false;
        }

        public static void Log(string text, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }
    }
}
