using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using static System.Net.Mime.MediaTypeNames;
using Discord.Net;
using Newtonsoft.Json;
using System.Data;
using System.Text.RegularExpressions;
using System.Reflection.Metadata;
using System.Net;

namespace CharacterAI_Discord_Bot
{
    public class Integration
    {
        private HttpClient _httpClient = new HttpClient();
        private Character _charInfo = new Character();
        private string _authToken;
        private string _historyExternalId;
        
        public bool Setup(string id, string token)
        {
            _charInfo.CharId = id;
            _authToken = token;

            if (!GetInfo()) return false;
            if (!GetHistory())
            {
                if (!CreateDialog())
                    return false;
            }
            Console.WriteLine($"СharacterAI - {_charInfo.Name}\n {_charInfo.Greeting}\n");

            return true;
        }

        public string CallCharacter(string msg)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/streaming/");
            request.Headers.Add("Authorization", $"Token {_authToken}");
            request.Headers.Add("ContentType", "application/json");

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "character_external_id", _charInfo.CharId },
                { "history_external_id", _historyExternalId },
                { "text", msg },
                { "tgt", _charInfo.Tgt },
                { "ranking_method", "random" }
            });

            var response = _httpClient.Send(request);
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
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/character/info/");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "external_id", _charInfo.CharId } })
            request.Headers.Add("Authorization", $"Token {_authToken}");
            request.Headers.Add("ContentType", "application/json");

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return false;

            var content = response.Content.ReadAsStringAsync().Result;
            var charParsed = JsonConvert.DeserializeObject<dynamic>(content).character;

            _charInfo.Name = charParsed.name;
            _charInfo.Greeting = charParsed.greeting;
            _charInfo.Tgt = charParsed.participant__user__username;

            return true;
        }

        private bool GetHistory()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/history/continue/");
            request.Headers.Add("Authorization", $"Token {_authToken}");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "character_external_id", _charInfo.CharId } });

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return false;

            var content = response.Content.ReadAsStringAsync().Result;
            var historyInfo = JsonConvert.DeserializeObject<dynamic>(content);

            // if there's status field, then response is "status: No Such History"
            if (historyInfo.status != null) return false;
            
            _historyExternalId = historyInfo.external_id;
            return true;
        }

        private bool CreateDialog()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/history/create/");
            request.Headers.Add("Authorization", $"Token {_authToken}");
            request.Headers.Add("Accept", "application/json, text/plain, */*");

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "character_external_id", _charInfo.CharId }
            });

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return false;

            var content = response.Content.ReadAsStringAsync().Result;
            _historyExternalId = JsonConvert.DeserializeObject<dynamic>(content).external_id;

            return true;
        }
    }
}