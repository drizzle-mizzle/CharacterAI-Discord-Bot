using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using Discord.Net;
using Newtonsoft.Json;
using System.Data;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using static System.Net.Mime.MediaTypeNames;


namespace CharacterAI_Discord_Bot
{
    public class Integration
    {
        private HttpClient httpClient = new HttpClient();
        private Character charInfo = new Character();
        private string authToken;
        private string historyExternalId;

        public void Setup(string id, string token)
        {
            this.charInfo.charId = id;
            this.authToken = token;
            GetInfo();
            if (!GetHistory()) CreateDialog();
            Console.WriteLine("СharacterAI - ready");
        }

        public string CallCharacter(string msg)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/streaming/");
            request.Headers.Add("Authorization", $"Token {authToken}");
            request.Headers.Add("ContentType", "application/json");

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "character_external_id", this.charInfo.charId },
                { "history_external_id", historyExternalId },
                { "text", msg },
                { "tgt", this.charInfo.tgt },
                { "ranking_method", "random" }
            });

            var response = httpClient.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            Random rand = new Random();
            // Персонаж отвечает сразу несколькими вариациями, и API присылает ответы одновременно по частям.
            // Последняя часть с полностью готовыми ответами всегда находится в предпоследней строке.
            var reply = JsonConvert.DeserializeObject<dynamic>(content.Split("\n")[^2]).replies[0];
            string replyText = reply.text;
            replyText = Regex.Replace(replyText, @"(\n){3,}", "\n\n");
            //SetPrimary(reply.id.ToString());

            return replyText;
        }
        private void GetInfo()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/character/info/");
            request.Headers.Add("authorization", $"Token {authToken}");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "external_id", this.charInfo.charId } });

            var response = httpClient.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;
            var character = JsonConvert.DeserializeObject<dynamic>(content).character;

            charInfo.name = character.name;
            charInfo.greeting = character.greeting;
            charInfo.tgt = character.participant__user__username;
        }

        private bool GetHistory()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/history/continue/");
            request.Headers.Add("authorization", $"Token {authToken}");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "character_external_id", charInfo.charId } });

            var response = httpClient.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;
            var historyInfo = JsonConvert.DeserializeObject<dynamic>(content);

            // если есть поле status, то значит пришёл ответ "status": "No Such History"
            if (historyInfo.status != null)
            {
                return false;
            }
            else
            {
                historyExternalId = historyInfo.external_id;
                return true;
            }
        }

        private void CreateDialog()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/history/create/");
            request.Headers.Add("Authorization", $"Token {authToken}");
            request.Headers.Add("Accept", "application/json, text/plain, */*");

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "character_external_id", this.charInfo.charId }
            });

            var response = httpClient.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            historyExternalId = JsonConvert.DeserializeObject<dynamic>(content).external_id;
        }

        // Currently unneeded
        //private void SetPrimary(string replyId)
        //{
        //    var request = new HttpRequestMessage(HttpMethod.Post, "https://beta.character.ai/chat/msg/update/primary/");
        //    request.Headers.Add("Authorization", $"Token {authToken}");
        //    request.Headers.Add("ContentType", "application/json");

        //    request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        //    {
        //        { "message_id", replyId },
        //        { "reason", "SWIPE" }
        //    });

        //    httpClient.Send(request);
        //    Console.WriteLine($"{replyId} set as primary");
        //}

        public class Character
        {
            public string charId;
            public string name;
            public string greeting;
            public string tgt;
        }

    }
}