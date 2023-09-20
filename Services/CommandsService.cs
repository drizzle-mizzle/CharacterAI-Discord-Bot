using Discord;
using Discord.Interactions;
using static CharacterAiDiscordBot.Services.CommonService;
using static CharacterAiDiscordBot.Services.StorageContext;
using CharacterAiDiscordBot.Models.Common;
using Discord.WebSocket;

namespace CharacterAiDiscordBot.Services
{
    public static partial class CommandsService
    {
        internal static string GetBestName(this SocketUser user)
        {
            if (user is SocketGuildUser gu)
                return gu.Nickname ?? gu.DisplayName ?? gu.Username;
            else
                return user.GlobalName ?? user.Username;
        }
        internal static bool IsHoster(this SocketUser? user)
        {
            string? hosterId = ConfigFile.HosterDiscordID.Value;

            try
            {
                return hosterId is not null && user is not null && user.Id == ulong.Parse(hosterId);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                return false;
            }
        }

        internal static bool IsServerOwner(this SocketGuildUser? user)
            => user is not null && user.Id == user.Guild.OwnerId;

        internal static bool HasManagerRole(this SocketGuildUser? user)
            => user is not null && user.Roles.Any(r => r.Name == ConfigFile.DiscordBotRole.Value);

        internal static async Task SendNoPowerFileAsync(this IInteractionContext context)
        {
            try
            {
                await context.Interaction.DeferAsync();
                var filename = ConfigFile.NoPermissionFile.Value;
                if (filename is null) return;

                var stream = File.OpenRead($"{EXE_DIR}{SC}storage{SC}{filename}");
                await context.Interaction.FollowupWithFileAsync(stream, filename);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
            }
        }

        internal static Embed CharacterInfoEmbed(Character character)
        {
            var emb = new EmbedBuilder().WithColor(Color.Gold);

            emb.WithTitle($"{OK_SIGN_DISCORD} **{character.Name}**");
            emb.WithFooter($"Created by {character.AuthorName}");

            var (link, stat) = ($"[Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})", $"Interactions: **{character.Interactions}**");

            string title = character.Title ?? "No title";
            title = (title.Length > 1000 ? title[0..1000] + "[...]" : title).Replace("\n\n", "\n");
            
            string desc = character.Description ?? "No description";
            desc = (desc.Length > 2500 ? desc[0..2500] + "[...]" : desc).Replace("\n\n", "\n");

            emb.WithDescription($"*\"{title}\"*\n\n{desc}");
            emb.AddField("Details", $"*Original link: {link}\nCan generate images: **{(character.ImageGenEnabled is true ? "Yes" : "No")}**\n{stat}*");

            if (!string.IsNullOrWhiteSpace(character.AvatarUrl))
                emb.WithImageUrl(character.AvatarUrl);

            return emb.Build();
        }

        /// <summary>
        /// Creates and sends character selection menu
        /// </summary>
        /// <returns>SearchQuery object linked to the created selection menu</returns>
        internal static async Task<SearchQuery?> BuildAndSendSelectionMenuAsync(InteractionContext context, SearchQueryData searchQueryData)
        {
            if (!searchQueryData.IsSuccessful)
            {
                await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} Failed to find a character: `{searchQueryData.ErrorReason}`".ToInlineEmbed(Color.Red));
                return null;
            }

            if (searchQueryData.IsEmpty)
            {
                await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} No characters were found".ToInlineEmbed(Color.Orange));
                return null;
            }

            await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild?.Id);

            int pages = (int)Math.Ceiling(searchQueryData.Characters.Count / 10.0f);
            var query = new SearchQuery(context.Interaction.Id, context.User.Id, searchQueryData, pages);
            var list = BuildCharactersList(query);
            var buttons = BuildSelectButtons(query);
            await context.Interaction.ModifyOriginalResponseAsync(msg => { msg.Embed = list; msg.Components = buttons; });

            return query; // further logic is handled by the ButtonsAndReactionsHandler()
        }

        public static Embed BuildCharactersList(SearchQuery query)
        {
            var list = new EmbedBuilder().WithTitle($"({query.SearchQueryData.Characters.Count}) Characters found by query \"{query.SearchQueryData.OriginalQuery}\":")
                                         .WithFooter($"Page {query.CurrentPage}/{query.Pages}")
                                         .WithColor(Color.Green);
            // Fill with first 10 or less
            int tail = query.SearchQueryData.Characters.Count - (query.CurrentPage - 1) * 10;
            int rows = tail > 10 ? 10 : tail;

            for (int i = 0; i < rows; i++)
            {
                int index = (query.CurrentPage - 1) * 10 + i;
                var character = query.SearchQueryData.Characters[index];
                string fTitle = character.Name!;

                if (i + 1 == query.CurrentRow) fTitle += " - ✅";

                string interactionsOrStars = $"Interactions: {character.Interactions}";

                list.AddField($"{index + 1}. {fTitle}", $"{interactionsOrStars} | Author: {character.AuthorName}");
            }

            return list.Build();
        }

        private static MessageComponent BuildSelectButtons(SearchQuery query)
        {
            // List navigation buttons
            var buttons = new ComponentBuilder()
                .WithButton(emote: new Emoji("\u2B06"), customId: $"up", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2B07"), customId: $"down", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2705"), customId: $"select", style: ButtonStyle.Success);
            // Pages navigation buttons
            if (query.Pages > 1) buttons
                .WithButton(emote: new Emoji("\u2B05"), customId: $"left", row: 1)
                .WithButton(emote: new Emoji("\u27A1"), customId: $"right", row: 1);

            return buttons.Build();
        }

        public static async Task TryToSetCharacterAvatarAsync(Character character, SocketSelfUser user, HttpClient client)
        {
            var imageStream = await TryDownloadImgAsync(character.AvatarUrl, client);
            imageStream ??= File.OpenRead($"{EXE_DIR}{SC}storage{SC}default_avatar.png");

            await user.ModifyAsync(u => u.Avatar = new Discord.Image(imageStream));
            imageStream.Dispose();
        }

        public static void TryToReportInLogsChannel(IDiscordClient client, string title, string desc, string? content, Color color, bool error)
        {
            _ = Task.Run(async () =>
            {
                string? channelId = null;

                if (error) channelId = ConfigFile.DiscordErrorLogsChannelID.Value;
                if (channelId.IsEmpty()) channelId = ConfigFile.DiscordLogsChannelID.Value;
                if (channelId.IsEmpty()) return;

                if (!ulong.TryParse(channelId, out var uChannelId)) return;

                var channel = await client.GetChannelAsync(uChannelId);
                if (channel is not ITextChannel textChannel) return;

                await ReportInLogsChannel(textChannel, title, desc, content, color);
            });
        }

        public static async Task ReportInLogsChannel(ITextChannel channel, string title, string desc, string? content, Color color)
        { 
            try
            {
                var embed = new EmbedBuilder().WithTitle(title).WithColor(color);

                if (content is not null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        if (content.Length > 1010)
                        {
                            embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~", $"```cs\n{content[0..1009]}...```");
                            content = content[1009..];
                        }
                        else
                        {
                            embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~", $"```cs\n{content}```");
                            break;
                        }
                    }
                }

                await channel.SendMessageAsync(embed: embed.WithDescription(desc).Build());
            }
            catch (Exception e)
            {
                LogException(new[] { e });
            }
        }
    }
}
