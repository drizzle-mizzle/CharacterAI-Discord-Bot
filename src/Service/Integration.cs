using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;

namespace CharacterAI_Discord_Bot.Service
{
    public class Integration : CommonService
    {
        public Character _charInfo = new();
        private readonly HttpClient _httpClient = new();
        private readonly string? _userToken;
        private string? _historyExternalId;
        public bool audienceMode = false;

        public Integration(string userToken)
        {
            _userToken = userToken;
        }

        public bool Setup(string charID = "")
        {
            if (!GetInfo(charID)) return false;
            if (!GetHistory()) return false;
            DownloadAvatar();

            Log("CharacterAI");
            Log(" - Connected\n\n", ConsoleColor.Yellow);
            Log($"  [{_charInfo.Name}]\n", ConsoleColor.Cyan);
            Log($"  {_charInfo.Greeting.Replace("\n", "\n  ")}\n\n");

            return true;
        }

        public string CallCharacter(string msg)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/streaming/")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "character_external_id", _charInfo.CharID },
                    { "enable_tti", "true" },
                    { "history_external_id", _historyExternalId },
                    { "text", msg },
                    { "tgt", _charInfo.Tgt },
                    { "ranking_method", "random" }
                })
            };
            request = SetHeaders(request);
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("accept-encoding", "gzip, deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorMsg = $"\nFailed to send message!\nDetails: {response}\nContent: {response.Content.ReadAsStringAsync().Result}\n\n" +
                    $"Message: {request.Content.ReadAsStringAsync().Result}";
                Log(errorMsg, ConsoleColor.Red);

                return "⚠️ Failed to send message!";
            }
            request.Dispose();

            var content = response.Content.ReadAsStringAsync().Result;
            // The character answers with many reply variations at once, and API sends them part by part so it could
            // be desplayed on site in real time with "typing" animation.
            // Last part with a list of complete replies always lies in a penult line of response content.
            var reply = JsonConvert.DeserializeObject<dynamic>(content.Split("\n")[^2]).replies[0];
            string replyText = reply.text;
            replyText = Regex.Replace(replyText, @"(\n){3,}", "\n\n"); // (3 or more) "\n\n\n..." -> (2) "\n\n"

            return replyText;
        }

        private bool GetInfo(string charID)
        {
            Log("Fetching character info... ");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/character/info/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "external_id", charID } });
            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/character/info/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            var charParsed = JsonConvert.DeserializeObject<dynamic>(content).character;

            _charInfo.CharID = charParsed.external_id;
            _charInfo.Name = charParsed.name;
            _charInfo.Greeting = charParsed.greeting;
            _charInfo.Tgt = charParsed.participant__user__username;
            _charInfo.AvatarUrl = $"https://characterai.io/i/400/static/avatars/{charParsed.avatar_file_name}";

            return Success("OK\n");
        }

        private bool GetHistory()
        {
            Log("Fetching chat history... ");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/history/continue/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "character_external_id", _charInfo.CharID } });
            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/continue/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            var historyInfo = JsonConvert.DeserializeObject<dynamic>(content);

            // if there's status field, then response is "status: No Such History"
            if (historyInfo.status == null) _historyExternalId = historyInfo.external_id;
            else if (!CreateDialog()) return Failure();

            return Success("OK\n");
        }

        private bool CreateDialog()
        {
            Log("No chat history found\n", ConsoleColor.Magenta);
            Log("Creating new dialog... ");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/history/create/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "character_external_id", _charInfo.CharID } });
            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/create/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            _historyExternalId = JsonConvert.DeserializeObject<dynamic>(content).external_id;

            return Success();
        }

        private bool DownloadAvatar()
        {
            Log("Downloading character avatar... ");
            using var request = new HttpRequestMessage(HttpMethod.Get, _charInfo.AvatarUrl);
            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
                return Failure($"Error!\n Request failed! ({_charInfo.AvatarUrl})\n");

            var content = response.Content.ReadAsStreamAsync().Result;
            using var avatar = File.Create(botImgPath);
            content.CopyTo(avatar);

            return Success("OK\n");
        }

        private static bool Success(string logText = "")
        {
            Log(logText, ConsoleColor.Green);

            return true;
        }

        private bool Failure(string logText = "")
        {
            Log(logText, ConsoleColor.Red);

            return false;
        }

        private HttpRequestMessage SetHeaders(HttpRequestMessage request)
        {
            var headers = new string[]
            {
                "accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
                "Authorization", $"Token {_userToken}",
                "ContentLength", request.Content.ToString().Length.ToString(),
                "ContentType", "application/json",
                "dnt", "1",
                "Origin", "https://beta.character.ai",
                "Referer", $"https://beta.character.ai/chat?char={_charInfo.CharID}",
                "sec-ch-ua", "\"Not?A_Brand\";v=\"8\", \"Chromium\";v=\"108\", \"Google Chrome\";v=\"108\"",
                "sec-ch-ua-mobile", "?0",
                "sec-ch-ua-platform", "Windows",
                "sec-fetch-dest", "empty",
                "sec-fetch-mode", "cors",
                "sec-fetch-site", "same-origin",
                "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36"
            };
            for (int i = 0; i < headers.Length-1; i+=2)
                request.Headers.Add(headers[i], headers[i+1]);

            return request;
        }
    }
}