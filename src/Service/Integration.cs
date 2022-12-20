using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace CharacterAI_Discord_Bot.Service
{
    public class Integration : CommonService
    {
        public Character charInfo = new();
        private readonly HttpClient _httpClient = new();
        private readonly string? _userToken;
        private string? _historyExternalId;
        public bool audienceMode = false;

        public Integration(string userToken)
        {
            _userToken = userToken;
        }

        public bool Setup(string charID)
        {
            if (!GetInfo(charID)) return false;
            if (!GetHistory()) return false;
            DownloadAvatar();

            Log("CharacterAI");
            Log(" - Connected\n\n", ConsoleColor.Yellow);
            Log($" [{charInfo.Name}]\n", ConsoleColor.Cyan);
            Log($"{charInfo.Greeting}\n{charInfo.Description}\n\n");

            return true;
        }

        public string[] CallCharacter(string msg, string imgPath)
        {
            var contentDict = new Dictionary<string, string>
            {
                { "character_external_id", charInfo.CharID },
                { "enable_tti", "true" },
                { "history_external_id", _historyExternalId },
                { "text", msg },
                { "tgt", charInfo.Tgt },
                { "ranking_method", "random" },
                { "staging", "false" }
            };
            if (!string.IsNullOrEmpty(imgPath))
            {
                var imgParams = new string[] {
                    "image_description_type", "AUTO_IMAGE_CAPTIONING",
                    "image_origin_type", "UPLOADED",
                    "image_rel_path", imgPath
                };
                for (var i = 0; i < imgParams.Length-1; i+=2)
                    contentDict.Add(imgParams[i], imgParams[i+1]);
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/streaming/")
            {
                Content = new FormUrlEncodedContent(contentDict)
            };

            request = SetHeaders(request);
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("accept-encoding", "gzip, deflate, br");
            using var response = _httpClient.Send(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorMsg = $"\nFailed to send message!\nDetails: {response}\nContent: {response.Content.ReadAsStringAsync().Result}\n\n" +
                    $"Message: {request.Content.ReadAsStringAsync().Result}";
                Failure(errorMsg);

                return new string[2] { "⚠️ Failed to send message!", "" };
            }
            request.Dispose();

            var content = response.Content.ReadAsStringAsync().Result;
            // The character answers with many reply variations at once, and API sends them part by part so it could
            // be desplayed on site in real time with "typing" animation.
            // Last part with a list of complete replies always lies in a penult line of response content.
            try { var reply = JsonConvert.DeserializeObject<dynamic>(content.Split("\n")[^2]).replies[0]; }
            catch { return new string[2] { "⚠️ Something went wrong...", "" }; }

            string replyText = reply.text;
            string replyImage = reply.image_rel_path ??= "";
            replyText = Regex.Replace(replyText, @"(\n){3,}", "\n\n"); // (3 or more) "\n\n\n..." -> (exactly 2) "\n\n"

            return new string[] { replyText, replyImage };
        }

        private bool GetInfo(string charID)
        {
            Log("Fetching character info... ");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/character/info/")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "external_id", charID } })
            };
            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/character/info/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            try { var charParsed = JsonConvert.DeserializeObject<dynamic>(content).character; }
            catch { return Failure("Something went wrong"); }

            charInfo.CharID = charParsed.external_id;
            charInfo.Name = charParsed.name;
            charInfo.Greeting = charParsed.greeting;
            charInfo.Description = charParsed.description;
            charInfo.Title = charParsed.title;
            charInfo.Tgt = charParsed.participant__user__username;
            charInfo.AvatarUrl = $"https://characterai.io/i/400/static/avatars/{charParsed.avatar_file_name}";

            return Success("OK\n");
        }

        private bool GetHistory()
        {
            Log("Fetching chat history... ");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/history/continue/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "character_external_id", charInfo.CharID } });
            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/continue/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            dynamic historyInfo;

            try { historyInfo = JsonConvert.DeserializeObject<dynamic>(content); }
            catch { return Failure("Something went wrong"); }

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
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "character_external_id", charInfo.CharID } });
            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/create/)\n");

            var content = response.Content.ReadAsStringAsync().Result;
            try { _historyExternalId = JsonConvert.DeserializeObject<dynamic>(content).external_id; }
            catch { return Failure("Something went wrong"); }

            return Success();
        }

        private bool DownloadAvatar()
        {
            Log("Downloading character avatar... ");

            Stream image;
            using var response = _httpClient.GetAsync(charInfo.AvatarUrl).Result;

            if (!response.IsSuccessStatusCode)
            {
                Failure($"Error!\n Request failed! ({charInfo.AvatarUrl})\n");
                Log("(Default avatar is used) ", ConsoleColor.DarkCyan);
                try { image = new FileStream(pfpDefaultPath, FileMode.Open); }
                catch { return Failure("Something went wrong"); }
            }
            else
            {
                image = response.Content.ReadAsStreamAsync().Result;
            }

            if (File.Exists(pfpPath)) File.Delete(pfpPath);

            try
            {
                using var avatar = File.Create(pfpPath);
                image.CopyTo(avatar);
                avatar.Close(); image.Close();
            }
            catch { return Failure("Something went wrong"); }

            return Success("OK\n");
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
                "Referer", $"https://beta.character.ai/chat?char={charInfo.CharID}",
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

        // in work
        public string UploadImg(byte[] img)
        {
            var bacImg = new ByteArrayContent(img);
            bacImg.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/upload-image/") 
            { 
                Content = new MultipartFormDataContent { { bacImg, "\"image\"", $"\"image.jpg\"" } }
            };

            request = SetHeaders(request);
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                Failure("\nRequest failed! (https://beta.character.ai/chat/upload-image/)\n");

                return "";
            }
            var content = response.Content.ReadAsStringAsync().Result;

            try { return JsonConvert.DeserializeObject<dynamic>(content).value; }
            catch { Failure("Something went wrong"); return ""; }
        }
    }
}