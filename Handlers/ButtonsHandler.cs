using Discord;
using Discord.WebSocket;
using CharacterAiDiscordBot.Services;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.CommandsService;
using static CharacterAiDiscordBot.Services.IntegrationService;
using Microsoft.Extensions.DependencyInjection;
namespace CharacterAiDiscordBot.Handlers
{
    internal class ButtonsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationService _integration;

        public ButtonsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.ButtonExecuted += (component) =>
            {
                Task.Run(async () => {
                    try { await HandleButtonAsync(component); }
                    catch (Exception e) { await HandleButtonException(component, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleButtonException(SocketMessageComponent component, Exception e)
        {
            LogException(new[] { e });
            var channel = component.Channel as IGuildChannel;
            var guild = channel?.Guild;
            var owner = guild is null ? null : (await guild.GetOwnerAsync()) as SocketGuildUser;

            TryToReportInLogsChannel(_client, title: "Button Exception",
                                              desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{owner?.GetBestName()} ({owner?.Username})`\n" +
                                                    $"Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                    $"User: `{component.User?.Username}`\n" +
                                                    $"Button ID: `{component.Data.CustomId}`",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }

        private async Task HandleButtonAsync(SocketMessageComponent component)
        {
            await component.DeferAsync();

            var searchQuery = _integration.LastSearchQuery;
            if (searchQuery is null || searchQuery.SearchQueryData.IsEmpty) return;
            if (searchQuery.AuthorId != component.User.Id) return;
            if (await UserIsBannedCheckOnly(component.User.Id)) return;

            int tail = searchQuery.SearchQueryData.Characters.Count - (searchQuery.CurrentPage - 1) * 10;
            int maxRow = tail > 10 ? 10 : tail;

            switch (component.Data.CustomId)
            {
                case "up":
                    if (searchQuery.CurrentRow == 1) searchQuery.CurrentRow = maxRow;
                    else searchQuery.CurrentRow--; break;
                case "down":
                    if (searchQuery.CurrentRow > maxRow) searchQuery.CurrentRow = 1;
                    else searchQuery.CurrentRow++; break;
                case "left":
                    searchQuery.CurrentRow = 1;
                    if (searchQuery.CurrentPage == 1) searchQuery.CurrentPage = searchQuery.Pages;
                    else searchQuery.CurrentPage--; break;
                case "right":
                    searchQuery.CurrentRow = 1;
                    if (searchQuery.CurrentPage == searchQuery.Pages) searchQuery.CurrentPage = 1;
                    else searchQuery.CurrentPage++; break;
                case "select":
                    try
                    {
                        await component.Message.ModifyAsync(msg =>
                        {
                            msg.Embed = WAIT_MESSAGE;
                            msg.Components = null;
                        });
                    }
                    catch { return; }

                    int index = (searchQuery.CurrentPage - 1) * 10 + searchQuery.CurrentRow - 1;
                    string characterId = searchQuery.SearchQueryData.Characters[index].Id;

                    var character = await _integration.CaiClient.GetInfoAsync(characterId, _integration.CaiAuthToken, _integration.CaiPlusMode);
                    if (character is null || character.IsEmpty)
                    {
                        await component.Message.ModifyAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} Failed to set a character".ToInlineEmbed(Color.Red));
                        return;
                    }

                    _integration.SelfCharacter = new()
                    {
                        Id = characterId,
                        Name = character.Name ?? "character",
                        AuthorName = character.Author ?? "unknown",
                        AvatarUrl = character.AvatarUrlFull ?? character.AvatarUrlMini,
                        Title = character.Title,
                        Description = character.Description ?? "No description",
                        Greeting = character.Greeting ?? $"*{character.Name} has joined the chat*",
                        ImageGenEnabled = character.ImageGenEnabled ?? false,
                        Interactions = character.Interactions ?? 0,
                        Tgt = character.Tgt!,
                    };

                    await component.Message.ModifyAsync(msg => msg.Embed = CharacterInfoEmbed(_integration.SelfCharacter));

                    string lastCharacterIdPath = $"{EXE_DIR}{SC}storage{SC}settings{SC}last_character.txt";
                    File.WriteAllText(lastCharacterIdPath, characterId);

                    await TryToSetCharacterAvatarAsync(_integration.SelfCharacter, _client.CurrentUser, _integration.HttpClient);
                    await component.Channel.SendMessageAsync(_integration.SelfCharacter.Greeting);

                    await Parallel.ForEachAsync(_client.Guilds, async (guild, ct) =>
                    {
                        try
                        {
                            await guild.CurrentUser.ModifyAsync(u => u.Nickname = character.Name);
                        }
                        catch
                        {
                            return;
                        }
                    });

                    _integration.LastSearchQuery = null;

                    return;
                default:
                    return;
            }

            try
            {   // Only if left/right/up/down is selected, either this line will never be reached
                await component.Message.ModifyAsync(c => c.Embed = BuildCharactersList(searchQuery)).ConfigureAwait(false);
            }
            catch { return; }
        }

    }
}
