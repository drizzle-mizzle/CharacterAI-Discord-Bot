using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace CharacterAI_Discord_Bot.Service
{
    public partial class Integration : CommonService
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

        public async Task<bool> Setup(string charID)
        {
            Log("\n" + new string ('>', 50), ConsoleColor.Green);
            Log("\nStarting character setup...\n", ConsoleColor.Yellow);
            Log("(ID: " + charID + ")\n\n", ConsoleColor.DarkMagenta);
            charInfo.CharID = charID;

            if (!await GetInfo()) return Failure($"\nSetup has been aborted\n{new string('<', 50)}\n");
            if (!await GetHistory()) return Failure($"\nSetup has been aborted\n{new string('<', 50)}\n");
            await DownloadAvatar();

            Log("\nCharacterAI - Connected\n\n", ConsoleColor.Green);
            Log($" [{charInfo.Name}]\n\n", ConsoleColor.Cyan);
            Log($"{charInfo.Greeting}\n"); 
            if (!string.IsNullOrEmpty(charInfo.Description))
                Log($"\"{charInfo.Description}\"\n");
            Log("\nSetup complete\n", ConsoleColor.Yellow);

            return Success(new string('<', 50) + "\n");
        }

        public async Task<string[]> CallCharacter(string msg, string imgPath)
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
            request.Headers.Add("accept-encoding", "gzip, deflate, br");
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorMsg = "\nFailed to send message!\n" +
                    $"Details: { response }\n" +
                    $"Response: { await response.Content.ReadAsStringAsync() }\n" +
                    $"Request: { await request.Content.ReadAsStringAsync() }";
                Failure(errorMsg);

                return new string[2] { "⚠️ Failed to send message!", "" };
            }
            request.Dispose();

            // The character answers with many reply variations at once, and API sends them part by part so it could
            // be desplayed on site in real time with "typing" animation.
            // Last part with a list of complete replies always lies in a penult line of response content.
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                var reply = JsonConvert.DeserializeObject<dynamic>(content.Split("\n")[^2]).replies[0];
                string replyText = reply.text;
                string replyImage = reply.image_rel_path ??= "";
                replyText = MyRegex().Replace(replyText, "\n\n");

                return new string[2] { replyText, replyImage };
            }

            catch { return new string[2] { "⚠️ Message has been sent successfully, but something went wrong...", "" }; }
        }

        private async Task<bool> GetInfo()
        {
            Log("Fetching character info... ");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/character/info/")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "external_id", charInfo.CharID } })
            };
            request = SetHeaders(request);
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Failure("Error!\n Request failed! (https://beta.character.ai/chat/character/info/)");

            try
            {
                var content = await response.Content.ReadAsStringAsync();
                var charParsed = JsonConvert.DeserializeObject<dynamic>(content).character;
                charInfo.Name = charParsed.name;
                charInfo.Greeting = charParsed.greeting;
                charInfo.Description = charParsed.description;
                charInfo.Title = charParsed.title;
                charInfo.Tgt = charParsed.participant__user__username;
                charInfo.AvatarUrl = $"https://characterai.io/i/400/static/avatars/{charParsed.avatar_file_name}";

                return Success("OK");
            }
            catch (Exception e) { return Failure("Something went wrong...\n" + e.ToString()); }
        }

        private async Task<bool> GetHistory()
        {
            Log("Fetching chat history... ");

            HttpRequestMessage request = new(HttpMethod.Post, "https://beta.character.ai/chat/history/continue/")
            {
                Content = new FormUrlEncodedContent(
                    new Dictionary<string, string> { { "character_external_id", charInfo.CharID } })
            };
            request = SetHeaders(request);
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/continue/)");

            var content = await response.Content.ReadAsStringAsync();
            dynamic historyInfo;

            try { historyInfo = JsonConvert.DeserializeObject<dynamic>(content); }
            catch (Exception e) { return Failure("Something went wrong...\n" + e.ToString()); }

            // If no status field, then history external_id is provided.
            if (historyInfo.status == null)
                _historyExternalId = historyInfo.external_id;
            // If there's status field, then response is "status: No Such History".
            else if (!await CreateNewDialog())
                return Failure();

            return Success("OK");
        }

        private async Task<bool> CreateNewDialog()
        {
            Log("No chat history found\n", ConsoleColor.Magenta);
            Log(" Creating new dialog... ", ConsoleColor.DarkCyan);

            HttpRequestMessage request = new(HttpMethod.Post, "https://beta.character.ai/chat/history/create/")
            {
                Content = new FormUrlEncodedContent(
                    new Dictionary<string, string> { { "character_external_id", charInfo.CharID } })
            };
            request = SetHeaders(request);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return Failure("Error!\n  Request failed! (https://beta.character.ai/chat/history/create/)");

            var content = await response.Content.ReadAsStringAsync();
            try { _historyExternalId = JsonConvert.DeserializeObject<dynamic>(content).external_id; }
            catch (Exception e) { return Failure("Something went wrong...\n" + e.ToString()); }

            return true;
        }

        private async Task<bool> DownloadAvatar()
        {
            Log("Downloading character avatar... ");
            Stream image;

            try { if (File.Exists(avatarPath)) File.Delete(avatarPath); }
            catch (Exception e) { return Failure("Something went wrong...\n" + e.ToString()); }

            using var avatar = File.Create(avatarPath);
            using var response = await _httpClient.GetAsync(charInfo.AvatarUrl);

            if (response.IsSuccessStatusCode)
            {
                try { image = await response.Content.ReadAsStreamAsync(); }
                catch (Exception e) { return Failure("Something went wrong...\n" + e.ToString()); }
            }
            else
            {
                Log($"Error! Request failed! ({charInfo.AvatarUrl})\n", ConsoleColor.Magenta);
                Log(" Setting default avatar... ", ConsoleColor.DarkCyan);

                try { image = new FileStream(defaultAvatarPath, FileMode.Open); }
                catch { return Failure($"Something went wrong.\n   Check if img/defaultAvatar.webp does exist."); }
            }
            image.CopyTo(avatar);
            image.Close();

            return Success("OK");
        }

        public async Task<string?> UploadImg(byte[] img)
        {
            var image = new ByteArrayContent(img);
            image.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/upload-image/")
            {
                Content = new MultipartFormDataContent { { image, "\"image\"", $"\"image.jpg\"" } }
            };

            request = SetHeaders(request);
            request.Headers.Add("accept-encoding", "deflate, br");

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                try { return JsonConvert.DeserializeObject<dynamic>(content).value.ToString(); }
                catch { Failure("Something went wrong"); return null; }
            }

            Failure("\nRequest failed! (https://beta.character.ai/chat/upload-image/)\n");
            return null;
        }

        private HttpRequestMessage SetHeaders(HttpRequestMessage request)
        {
            var headers = new string[]
            {
                "Accept", "application/json, text/plain, */*",
                "Authorization", $"Token {_userToken}",
                "accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
                "accept-encoding", "deflate, br",
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


        // (3 or more) "\n\n\n..." -> (exactly 2) "\n\n"
        [GeneratedRegex("(\\n){3,}")]
        private static partial Regex MyRegex();
    }
}