using CharacterAiDiscordBot.Models.Database;
using Microsoft.EntityFrameworkCore;
using static CharacterAiDiscordBot.Services.CommonService;
using CharacterAiDiscordBot.Models.Common;

namespace CharacterAiDiscordBot.Services
{
    internal class StorageContext : DbContext
    {
        internal DbSet<BlockedGuild> BlockedGuilds { get; set; }
        internal DbSet<BlockedUser> BlockedUsers { get; set; }
        internal DbSet<Channel> Channels { get; set; }
        internal DbSet<Character> Characters { get; set; }
        internal DbSet<Guild> Guilds { get; set; }
        internal DbSet<HuntedUser> HuntedUsers { get; set; }

#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
        public StorageContext()
        {

        }
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={EXE_DIR}{SC}storage{SC}db.sqlite3").UseLazyLoadingProxies(true);
        }


        protected internal static async Task<Guild> FindOrStartTrackingGuildAsync(ulong guildId, StorageContext? db = null)
        {
            db ??= new StorageContext();
            var guild = await db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                guild = new() { Id = guildId };
                await db.Guilds.AddAsync(guild);
                await db.SaveChangesAsync();
                return await FindOrStartTrackingGuildAsync(guildId, db);
            }

            return guild;
        }

        protected internal static async Task<Channel> FindOrStartTrackingChannelAsync(ulong channelId, ulong? guildId, StorageContext? db = null)
        {
            db ??= new StorageContext();
            var channel = await db.Channels.FindAsync(channelId);

            if (channel is null)
            {
                channel = new()
                {
                    Id = channelId,
                    HistoryId = null,
                    RandomReplyChance = 0,
                    CurrentSwipeIndex = 0,
                    LastCallTime = DateTime.UtcNow,
                    MessagesSent = 0,
                    ResponseDelay = 1,
                    SkipNextBotMessage = false,
                    StopBtnEnabled = true,
                    SwipesEnabled = true,
                    GuildId = guildId is null ? null : (await FindOrStartTrackingGuildAsync((ulong)guildId, db)).Id
                };
                await db.Channels.AddAsync(channel);
                await db.SaveChangesAsync();

                return await FindOrStartTrackingChannelAsync(channelId, guildId, db);
            }

            return channel;
        }
    }
}
