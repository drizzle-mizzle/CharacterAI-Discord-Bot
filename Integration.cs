using CharacterAI_Discord_Bot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace CharacterAI_Discord_Bot.Service
{
    public class Integration : IntegrationService
    {
        public bool audienceMode;
        public Character charInfo = new();

        private readonly string? _userToken;
        private readonly HttpClient _httpClient = new();

        public Integration(string userToken)
            => _userToken = userToken;

        public async Task<bool> Setup(string charID, bool reset)
        {
            Log("\n" + new string ('>', 50), ConsoleColor.Green);
            Log("\nStarting character setup...\n", ConsoleColor.Yellow);
            Log("(ID: " + charID + ")\n\n", ConsoleColor.DarkMagenta);

            charInfo.CharId = charID;
            if (!await GetInfo() || !(reset ? await CreateNewDialog() : await GetHistory()))
                return Failure($"\nSetup has been aborted\n{ new string('<', 50) }\n");

            HelloLog(charInfo);

            return Success(new string('<', 50) + "\n");
        }

        public async Task<dynamic> CallCharacter(string msg, string imgPath, string? primaryMsgId = null, string? parentMsgId = null)
        {
            var dynamicContent = BasicCallContent(charInfo, msg, imgPath);

            // Fetch an new answer (swipe)
            if (parentMsgId is not null)
                dynamicContent.parent_msg_id = parentMsgId;
            // Reply to the new answer
            else if (primaryMsgId is not null)
            {
                dynamicContent.primary_msg_id = primaryMsgId;
                dynamicContent.seen_msg_ids = new string[] { primaryMsgId };
            }

            // Prepare request data
            var requestContent = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dynamicContent)));
            requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpRequestMessage request = new(HttpMethod.Post, "https://beta.character.ai/chat/streaming/");
            request.Content = requestContent;
            request.Headers.Add("accept-encoding", "gzip");
            request = SetHeaders(request);

            // Send message
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Failure("\nFailed to send message!\n" +
                        $"Details: {response}\n" +
                        $"Response: {await response.Content.ReadAsStringAsync()}\n" +
                        $"Request: {request?.Content?.ReadAsStringAsync().Result}");

                return "⚠️ Failed to send message!";
            }
            request.Dispose();

            // Get character answer
            string[] chunks = (await response.Content.ReadAsStringAsync()).Split("\n");
            string finalChunk;
            try { finalChunk = chunks.First(c => JsonConvert.DeserializeObject<dynamic>(c)!.is_final_chunk == true); }
            catch { return "⚠️ Message has been sent successfully, but something went wrong..."; }

            return JsonConvert.DeserializeObject<dynamic>(finalChunk)!;
        }

        private async Task<bool> GetInfo()
        {
            Log("Fetching character info... ");

            HttpRequestMessage request = new(HttpMethod.Post, "https://beta.character.ai/chat/character/info/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "external_id", charInfo.CharId! }
            });
            request = SetHeaders(request);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return Failure("Error!\n Request failed! (https://beta.character.ai/chat/character/info/)");

            var content = await response.Content.ReadAsStringAsync();
            var charParsed = JsonConvert.DeserializeObject<dynamic>(content)?.character;
            if (charParsed is null)
                return Failure("Something went wrong...");

            charInfo.Name = charParsed.name;
            charInfo.Greeting = charParsed.greeting;
            charInfo.Description = charParsed.description;
            charInfo.Title = charParsed.title;
            charInfo.Tgt = charParsed.participant__user__username;
            charInfo.AvatarUrl = $"https://characterai.io/i/400/static/avatars/{charParsed.avatar_file_name}";

            return Success("OK");
        }

        private async Task<bool> GetHistory()
        {
            Log("Fetching chat history... ");

            HttpRequestMessage request = new(HttpMethod.Post, "https://beta.character.ai/chat/history/continue/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "character_external_id", charInfo.CharId! }
            });
            request = SetHeaders(request);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return Failure("Error!\n Request failed! (https://beta.character.ai/chat/history/continue/)");

            var content = await response.Content.ReadAsStringAsync();
            var externalId = JsonConvert.DeserializeObject<dynamic>(content)?.external_id;
            if (externalId is null)
            {
                Log("No chat history found\n", ConsoleColor.Magenta);
                return await CreateNewDialog();
            }

            charInfo.HistoryExternalId = externalId;

            return Success($"OK\n(History ID: { charInfo.HistoryExternalId })");
        }

        private async Task<bool> CreateNewDialog()
        {
            Log(" Creating new dialog... ", ConsoleColor.DarkCyan);

            HttpRequestMessage request = new(HttpMethod.Post, "https://beta.character.ai/chat/history/create/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { 
                { "character_external_id", charInfo.CharId! },
                { "override_history_set", null! }
            });
            request = SetHeaders(request);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return Failure("Error!\n  Request failed! (https://beta.character.ai/chat/history/create/)");

            var content = await response.Content.ReadAsStringAsync();
            var externalId = JsonConvert.DeserializeObject<dynamic>(content)?.external_id;
            if (externalId is null)
                return Failure("Something went wrong...");

            charInfo.HistoryExternalId = externalId;

            return Success($"OK\n(History ID: {charInfo.HistoryExternalId})");
        }

        public async Task<string?> UploadImg(byte[] img)
        {
            var image = new ByteArrayContent(img);
            image.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/upload-image/");
            request.Content = new MultipartFormDataContent { { image, "\"image\"", $"\"image.jpg\"" } };        
            request = SetHeaders(request);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Failure("\nRequest failed! (https://beta.character.ai/chat/upload-image/)\n");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            string? imgPath = JsonConvert.DeserializeObject<dynamic>(content)?.value;
            
            return imgPath;
        }

        public async Task<List<dynamic>?> Search(string text)
        {
            string url = $"https://beta.character.ai/chat/characters/search/?query={text}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request = SetHeaders(request);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Failure($"\nRequest failed! ({url})\n");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            JArray characters = JsonConvert.DeserializeObject<dynamic>(content)!.characters;

            return characters.HasValues ? characters.ToObject<List<dynamic>>() : null;
        }

        private HttpRequestMessage SetHeaders(HttpRequestMessage request)
        {
            var headers = new string[]
            {
                "Accept", "application/json, text/plain, */*",
                "Authorization", $"Token {_userToken}",
                "accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
                "accept-encoding", "deflate, br",
                "ContentType", "application/json",
                "dnt", "1",
                "Origin", "https://beta.character.ai",
                "Referer", $"https://beta.character.ai/" + (charInfo?.CharId is null ? "search?" : $"chat?char={charInfo.CharId}"),
                "sec-ch-ua", "\"Not?A_Brand\";v=\"8\", \"Chromium\";v=\"108\", \"Google Chrome\";v=\"108\"",
                "sec-ch-ua-mobile", "?0",
                "sec-ch-ua-platform", "Windows",
                "sec-fetch-dest", "empty",
                "sec-fetch-mode", "cors",
                "sec-fetch-site", "same-origin",
                "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36"
            };

            for (int i = 0; i < headers.Length-1; i+=2)
                request!.Headers.Add(headers[i], headers[i+1]);

            return request!;
        }
    }
}