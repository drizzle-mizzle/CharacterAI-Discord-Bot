namespace CharacterAI_Discord_Bot.Models
{
    public class HandlerTemps
    {
        public int replyChance = 0;
        public int huntChance = 100;
        public int skipMessages = 0;
        public ulong lastCharacterCallMsgId = 0;
        public List<ulong> blackList = new();
        public List<ulong> huntedUsers = new();
        public Dictionary<ulong, dynamic> userMsgCount = new();
    }
}
