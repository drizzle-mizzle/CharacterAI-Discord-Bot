using CharacterAiDiscordBot.Models.Common;

namespace CharacterAiDiscordBot.Services
{
    internal static partial class CommonService
    {
        internal static void Log(object? text)
        {
            Console.Write($"{text + (text is string ? "" : "\n")}");
        }

        internal static void LogGreen(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(text);
            Console.ResetColor();
        }
        internal static void LogRed(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(text);
            Console.ResetColor();
        }

        internal static void LogYellow(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Log(text);
            Console.ResetColor();
        }

        internal static void LogException(object?[]? text)
        {
            if (text is null) return;

            LogRed(new string('~', Console.WindowWidth - 1) + "\n");
            LogRed($"{string.Join('\n', text)}\n");
            LogRed(new string('~', Console.WindowWidth - 1) + "\n");
        }
    }
}
