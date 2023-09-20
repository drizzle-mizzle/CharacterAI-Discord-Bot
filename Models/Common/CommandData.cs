namespace CharacterAiDiscordBot.Models.Common
{
    internal class CommandData
    {
        internal string Name { get; }
        internal string? Description { get; set; }
        internal Dictionary<string, string>? NameLocals { get; set; }
        internal Dictionary<string, string>? DescLocals { get; set; }

        internal CommandData(string name)
        {
            Name = name;
        }
    }
}
