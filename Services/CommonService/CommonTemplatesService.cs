using Discord;

namespace CharacterAiDiscordBot.Services
{
    internal static partial class CommonService
    {
        internal static readonly string WARN_SIGN_UNICODE = "⚠";
        internal static readonly string WARN_SIGN_DISCORD = ":warning:";
        internal static readonly string OK_SIGN_DISCORD = ":white_check_mark: ";
        internal static Embed WAIT_MESSAGE = $"🕓 Wait...".ToInlineEmbed(new Color(154, 171, 182));
        internal static Emoji ARROW_LEFT = new("\u2B05");
        internal static Emoji ARROW_RIGHT = new("\u27A1");
        internal static Emoji STOP_BTN = new("\u26D4");
        internal static Emoji TRANSLATE_BTN = new("\uD83D\uDD24");
    }
}
