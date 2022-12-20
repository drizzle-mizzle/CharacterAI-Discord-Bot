using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterAI_Discord_Bot.Service
{
    public class CommonService
    {

        public string pfpPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "characterAvatar.avif";
        public string pfpDefaultPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "defaultAvatar.png";
        public string tempImgPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "temp.webp";
        public string nopowerPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar + "nopower.gif";


        public static async Task AutoSetup(ServiceProvider services, DiscordSocketClient client)
        {
            
            dynamic config = GetConfig();
            var integration = services.GetRequiredService<MessageHandler>().integration;
            if (!integration.Setup(config.autoCharID)) return;

            integration.audienceMode = config.autoAudienceMode;
            string desc = integration.charInfo.CharID == null ? "No character selected | " : $"Description: {integration.charInfo.Title} | ";
            await client.SetGameAsync(desc + $"Audience mode: " + (integration.audienceMode ? "✔️" : "✖️"));
        }

        // probably not useless
        //public static async Task CreateRole(DiscordSocketClient client)
        //{
        //    var guild = client.Guilds.FirstOrDefault();
        //    var role = client.GetGuild(guild.Id).Roles.FirstOrDefault(role => role.Name == CommonService.GetConfig().botRole);
        //    if (!string.IsNullOrEmpty(role.ToString)) return;

        //    try
        //    {
        //        Log("Creating role... ");
        //        var newRole = await guild.CreateRoleAsync(GetConfig().botRole).Result;
        //        await guild.Owner.AddRoleAsync(newRole);
        //    }
        //    catch { Failure("Failed to create default bot role. Probably, missing permissions?"); }

        //    Success("OK\n");
        //}

        public static dynamic GetConfig()
        {
            using StreamReader configJson = new StreamReader(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Config.json");
            var configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
            dynamic config = new
            {
                userToken = configParsed["char_ai_user_token"].Value<string>(),
                botToken = configParsed["discord_bot_token"].Value<string>(),
                botRole = configParsed["discord_bot_role"].Value<string>(),
                botPrefixes = JsonConvert.DeserializeObject<string[]>(configParsed["discord_bot_prefixes"].ToString()),
                autoSetup = bool.Parse(configParsed["auto_setup"].Value<string>()),
                autoCharID = configParsed["auto_char_id"].Value<string>(),
                autoAudienceMode = bool.Parse(configParsed["auto_audience_mode"].Value<string>())
            };

            return config;
        }


        public static string RemoveMention(string text)
        {
            var rgx = new Regex(@"\<(.*?)\>");
            text = rgx.Replace(text, "", 1);

            foreach(var prefix in GetConfig().botPrefixes)
                text.Replace(prefix, "");

            return text.Trim(' ');
        }

        public static byte[] DownloadImg(string url)
        {
            HttpClient client = new();

            while (true)
            {
                try { return client.GetByteArrayAsync(url).Result; }
                catch { Thread.Sleep(2000); }
            }
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
