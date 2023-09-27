using CharacterAiDiscordBot.Services;
using Microsoft.EntityFrameworkCore;
using static CharacterAiDiscordBot.Services.CommonService;

namespace CharacterAiDiscordBot
{
    internal class Program : DiscordService
    {
        static void Main()
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                string logstxt = $"{EXE_DIR}{SC}logs.txt";
                if (!File.Exists(logstxt)) File.Create(logstxt).Close();

                var sw = File.AppendText(logstxt);
                string text = $"{new string('~', 10)}\n" +
                              $"Sender: {s?.GetType()}\n" +
                              $"Error:\n{args?.ExceptionObject}\n";
                sw.WriteLine(text);
                sw.Close();
            };

            Log("Working directory: ");
            LogYellow(EXE_DIR + '\n');

            await new StorageContext().Database.MigrateAsync();
            await SetupDiscordClient();

            await Task.Delay(-1);
        }
    }
}