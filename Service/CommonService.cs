using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using Discord;
using CharacterAI_Discord_Bot.Models;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class CommonService
    {
        public static readonly dynamic Config = GetConfig()!;
        public static readonly string imgPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar;
        public static readonly string defaultAvatarPath = imgPath + "defaultAvatar.png";
        public static readonly string nopowerPath = imgPath + Config.nopower;

        public static Embed BuildCharactersList(List<dynamic> characters, int pages, string query, int row, int page)
        {
            var list = new EmbedBuilder()
                            .WithTitle($"Characters found by query \"{query}\":\n({characters.Count})\n")
                            .WithFooter($"Page {page}/{pages}");

            // Fill with first 10 or less
            int tail = characters.Count - (page - 1) * 10;
            for (int i = 0; i < (tail > 10 ? 10 : tail); i++)
            {
                int index = (page - 1) * 10 + i;
                var character = characters[index];

                string charName = character.participant__name;
                string charInts = character.participant__num_interactions;
                string author = character.user__username;

                string fTitle = charName;
                if (i+1 == row) fTitle += " - ✅";

                list.AddField($"{index + 1}. {fTitle}", $"Interactions: {charInts} | Author: {author}");
            }

            return list.Build();
        }

        public static string RemoveMention(string text)
        {
            text = text.Trim(' ');
            // Remove first mention
            if (text.StartsWith("<"))
                text = new Regex("\\<(.*?)\\>").Replace(text, "", 1);
            // Remove prefix
            foreach (string prefix in Config.botPrefixes)
                if (text.StartsWith(prefix))
                    text = text.Replace(prefix, "");

            return text;
        }

        public static async Task<byte[]?> TryDownloadImg(string url, int attempts)
        {
            if (string.IsNullOrEmpty(url)) return null;

            using HttpClient client = new();
            // Try n times and return null
            for (int i = 0; i < attempts; i++)
            {
                try { return await client.GetByteArrayAsync(url).ConfigureAwait(false); }
                catch { await Task.Delay(2500); }
            }

            return null;
        }

        public static async Task<bool> TryGetImage(string url)
        {
            using HttpClient client = new();

            for (int i = 0; i < 10; i++)
                if ((await client.GetAsync(url).ConfigureAwait(false)).IsSuccessStatusCode)
                    return true;
                else
                    await Task.Delay(2500);

            return false;
        }

        // Test feature that makes character aware that he's talking to many different people
        public static string MakeItThreadMessage(string text, SocketUserMessage message)
        {
            string author = message.Author.Username;
            if (!string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(text))
                text = $"User [{author}] says:\n{text}";
            if (message.ReferencedMessage != null)
                text = $"((In response to: \"{RemoveMention(message.ReferencedMessage.Content)}\"))\n{text}";

            return text;
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
            var path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Config.json";
            using StreamReader configJson = new(path);
            try
            {
                var configParsed = (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd())!;
                return new
                {
                    userToken = configParsed["char_ai_user_token"]!.Value<string>(),
                    botToken = configParsed["discord_bot_token"]!.Value<string>(),
                    botRole = configParsed["discord_bot_role"]!.Value<string>(),
                    botPrefixes = JsonConvert.DeserializeObject<string[]>(configParsed["discord_bot_prefixes"]!.ToString()),
                    defaultAudienceMode = bool.Parse(configParsed["default_audience_mode"]!.Value<string>()!),
                    nopower = configParsed["default_no_permission_file"]!.Value<string>(),
                    rateLimit = configParsed["rate_limit"]!.Value<int>(),
                    removeDelay = configParsed["buttons_remove_delay"]!.Value<int>(),
                    autoSetupEnabled = bool.Parse(configParsed["auto_setup"]!.Value<string>()!),
                    autoCharID = configParsed["auto_char_id"]!.Value<string>()
                };
            }
            catch
            {
                Failure("Something went wrong... Check your Config file.\n");
                return null;
            }
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
    }
}
