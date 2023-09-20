using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace CharacterAiDiscordBot.Services
{
    internal static partial class CommonService
    {
        // Simply checks whether image is avalable.
        // (cAI is used to have broken undownloadable images or sometimes it's just
        //  takes eternity for it to upload one on server, but image url is provided in advance)
        public static async Task<bool> TryGetImageAsync(string url, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            for (int i = 0; i < 10; i++)
                if ((await httpClient.GetAsync(url).ConfigureAwait(false)).IsSuccessStatusCode)
                    return true;
                else
                    await Task.Delay(3000);

            return false;
        }

        public static async Task<Stream?> TryDownloadImgAsync(string? url, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            for (int i = 0; i < 10; i++)
            {
                try {
                    var response = await httpClient.GetAsync(url).ConfigureAwait(false);
                    return await response.Content.ReadAsStreamAsync();
                }
                catch { await Task.Delay(3000); }
            }

            return null;
        }

        internal static Embed ToInlineEmbed(this string text, Color color, bool bold = true, string? imageUrl = null)
        {
            string desc = bold ? $"**{text}**" : text;

            var result = new EmbedBuilder().WithDescription(desc).WithColor(color);
            if (!string.IsNullOrWhiteSpace(imageUrl))
                result.WithImageUrl(imageUrl);

            return result.Build();
        }

        public static bool ToBool(this string? str)
            => bool.Parse(str ?? "false");

        public static int ToInt(this string str)
            => int.Parse(str);

        public static bool IsEmpty(this string? str)
            => string.IsNullOrWhiteSpace(str);


        public static string RemovePrefix(this string str, string prefix)
        {
            var result = str.Trim();
            if (result.StartsWith(prefix))
                result = result.Remove(0, prefix.Length);

            return result;
        }

        public static string AddRefQuote(this string str, IUserMessage? refMsg)
        {
            if (str.Contains("{{ref_msg_text}}"))
            {
                int start = str.IndexOf("{{ref_msg_begin}}");
                int end = str.IndexOf("{{ref_msg_end}}") + "{{ref_msg_end}}".Length;

                if (string.IsNullOrWhiteSpace(refMsg?.Content))
                {
                    str = str.Remove(start, end - start).Trim();
                }
                else
                {
                    string refName = refMsg.Author is SocketGuildUser refGuildUser ? (refGuildUser.GetBestName()) : refMsg.Author.Username;
                    string refContent = refMsg.Content.Replace("\n", " ");
                    if (refContent.StartsWith("<"))
                        refContent = MentionRegex().Replace(refContent, "", 1);

                    int refL = Math.Min(refContent.Length, 150);
                    str = str.Replace("{{ref_msg_user}}", refName)
                             .Replace("{{ref_msg_text}}", refContent[0..refL] + (refL == 150 ? "..." : ""))
                             .Replace("{{ref_msg_begin}}", "")
                             .Replace("{{ref_msg_end}}", "");
                }
            }

            return str;
        }

        [GeneratedRegex("\\<(.*?)\\>")]
        public static partial Regex MentionRegex();

    }
}
