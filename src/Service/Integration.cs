using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace CharacterAI_Discord_Bot.Service
{
    public class Integration
    {
        private readonly HttpClient _httpClient = new();
        public Character _charInfo = new();
        private string? _authToken;
        private string? _historyExternalId;

        public bool Setup(string userToken, string charID)
        {
            _authToken = userToken;
            _charInfo.CharID = charID;

            if (!GetInfo()) return false;
            if (!GetHistory()) return false;
            DownloadAvatar();

            Log("CharacterAI");
            Log(" - Connected\n\n", ConsoleColor.Yellow);
            Log($"  [{_charInfo.Name}]\n", ConsoleColor.Cyan);
            Log($"  {_charInfo.Greeting}\n\n");

            return true;
        }

        public string CallCharacter(string msg)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/streaming/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "character_external_id", _charInfo.CharID },
                { "enable_tti", "true" },
                { "history_external_id", _historyExternalId },
                { "text", msg },
                { "tgt", _charInfo.Tgt },
                { "ranking_method", "random" }
            });
            request = SetHeaders(request);
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("accept-encoding", "gzip, deflate, br");

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorMsg = $"\nFailed to send message!\nDetails: {response}\nContent: {response.Content.ReadAsStringAsync().Result}\n\n";
                Log(errorMsg, ConsoleColor.Red);
                return "⚠️ Failed to send message!";
            }

            var content = response.Content.ReadAsStringAsync().Result;
            // The character answers with many reply variations at once, and API sends them part by part so it could
            // be desplayed on site in real time with "typing" animation.
            // Last part with a list of complete replies always lies in a penult line of response content.
            var reply = JsonConvert.DeserializeObject<dynamic>(content.Split("\n")[^2]).replies[0];
            string replyText = reply.text;
            replyText = Regex.Replace(replyText, @"(\n){3,}", "\n\n"); // (3 or more) "\n\n\n..." -> (2) "\n\n"

            return replyText;
        }

        private bool GetInfo()
        {
            Log("Fetching character info... ");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/character/info/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "external_id", _charInfo.CharID } });
            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/character/info/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            var charParsed = JsonConvert.DeserializeObject<dynamic>(content).character;

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

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/continue/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            var historyInfo = JsonConvert.DeserializeObject<dynamic>(content);

            // if there's status field, then response is "status: No Such History"
            if (historyInfo.status != null) CreateDialog(); 
            else _historyExternalId = historyInfo.external_id;

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

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/create/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            _historyExternalId = JsonConvert.DeserializeObject<dynamic>(content).external_id;

            return Success();
        }

        private bool DownloadAvatar()
        {
            Log("Downloading character avatar... ");
            var request = new HttpRequestMessage(HttpMethod.Get, _charInfo.AvatarUrl);
            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure($"Error!\n Request failed! ({_charInfo.AvatarUrl})\n");

            using var content = response.Content.ReadAsStreamAsync().Result;
            using var avatar = File.Create("characterAvatar.avif");
            content.CopyTo(avatar);

            return Success("OK\n");
        }

        private bool Success(string logText = "")
        {
            Log(logText, ConsoleColor.Green);

            return true;
        }

        private bool Failure(string logText = "")
        {
            Log(logText, ConsoleColor.Red);

            return false;
        }


        private void Log(string text, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }

        private HttpRequestMessage SetHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            request.Headers.Add("Authorization", $"Token {_authToken}");
            request.Headers.Add("ContentLength", request.Content.ToString().Length.ToString());
            request.Headers.Add("ContentType", "application/json");
            request.Headers.Add("dnt", "1");
            request.Headers.Add("Origin", "https://beta.character.ai");
            request.Headers.Add("Referer", $"https://beta.character.ai/chat?char={_charInfo.CharID}");
            request.Headers.Add("sec-ch-ua", "\"Not?A_Brand\";v=\"8\", \"Chromium\";v=\"108\", \"Google Chrome\";v=\"108\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "Windows");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");

            return request;
        }
    }
}