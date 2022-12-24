using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class CommonService
    {
        public static readonly dynamic Config = GetConfig();
        public static readonly string imgPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar;
        public static readonly string avatarPath = imgPath + "characterAvatar.webp";
        public static readonly string defaultAvatarPath = imgPath + "defaultAvatar.webp";
        public static readonly string tempImgPath = imgPath + "temp.webp";
        public static readonly string nopowerPath = imgPath + Config.nopower;
        

        public static async Task AutoSetup(ServiceProvider services, DiscordSocketClient client)
        {
            if (Config is null) return;

            var integration = services.GetRequiredService<MessageHandler>().integration;
            integration.audienceMode = Config.defaultAudienceMode;
            if (!await integration.Setup(Config.autoCharID)) return;

            string desc = integration.charInfo.CharID == null ? "No character selected" : $"Description: {integration.charInfo.Title}";
            await client.SetGameAsync(desc + $" | Audience mode: " + (integration.audienceMode ? "✔️" : "✖️"));

            using var fs = new FileStream(avatarPath, FileMode.Open);
            await client.CurrentUser.ModifyAsync(u => { u.Avatar = new Discord.Image(fs); });

            var guildID = client.Guilds.First().Id;
            var botAsGuildUser = client.GetGuild(guildID).GetUser(client.CurrentUser.Id);
            await botAsGuildUser.ModifyAsync(u => { u.Nickname = integration.charInfo.Name; });
        }

        public static string RemoveMention(string text)
        {
            text = text.Trim(' ');
            // Remove first mention
            if (text.StartsWith("<"))
                text = MyRegex().Replace(text, "", 1);
            // Remove prefix
            foreach (string prefix in Config.botPrefixes)
                text = text.Replace(prefix, "");

            return text;
        }

        public static async Task<byte[]?> DownloadImg(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            HttpClient client = new();
            // Try 10 times and return null
            for (int i = 0; i < 10; i++)
            {
                try { return await client.GetByteArrayAsync(url); }
                catch { Thread.Sleep(2000); }
            }

            return null;
        }

        public static bool Success(string logText = "")
        {
            Log(logText + "\n", ConsoleColor.Green);

            return true;
        }

        public static bool Failure(string logText = "")
        {
            Log(logText + "\n", ConsoleColor.Red);

            return false;
        }

        public static void Log(string text, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }

        public static dynamic? GetConfig()
        {
            var path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "Config.json";
            using StreamReader configJson = new StreamReader(path);
            try
            {
                var configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
                return new
                {
                    userToken = configParsed["char_ai_user_token"].Value<string>(),
                    botToken = configParsed["discord_bot_token"].Value<string>(),
                    botRole = configParsed["discord_bot_role"].Value<string>(),
                    botPrefixes = JsonConvert.DeserializeObject<string[]>(configParsed["discord_bot_prefixes"].ToString()),
                    defaultAudienceMode = bool.Parse(configParsed["default_audience_mode"].Value<string>()),
                    nopower = configParsed["default_no_permission_file"].Value<string>(),
                    autoSetupEnabled = bool.Parse(configParsed["auto_setup"].Value<string>()),
                    autoCharID = configParsed["auto_char_id"].Value<string>()
                };
            }
            catch
            {
                Failure("Something went wrong... Check your Config file.\n");
                return null;
            }
        }

        [GeneratedRegex("\\<(.*?)\\>")]
        private static partial Regex MyRegex();

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
    }
}
