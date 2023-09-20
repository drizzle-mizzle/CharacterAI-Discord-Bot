﻿// <auto-generated />
using System;
using CharacterAiDiscordBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace CharacterAiDiscordBot.Migrations
{
    [DbContext(typeof(StorageContext))]
    partial class StorageContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.11")
                .HasAnnotation("Proxies:ChangeTracking", false)
                .HasAnnotation("Proxies:CheckEquality", false)
                .HasAnnotation("Proxies:LazyLoading", true);

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Common.Character", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("AuthorName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("AvatarUrl")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Greeting")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("ImageGenEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("Interactions")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Tgt")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Characters");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.BlockedGuild", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("BlockedGuilds");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.BlockedUser", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("From")
                        .HasColumnType("TEXT");

                    b.Property<ulong?>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Hours")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("BlockedUsers");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.Channel", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ChannelMessagesFormat")
                        .HasColumnType("TEXT");

                    b.Property<int>("CurrentSwipeIndex")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("HistoryId")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastCallTime")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("LastCharacterDiscordMsgId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LastCharacterMsgId")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("LastDiscordUserCallerId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LastUserMsgId")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("MessagesSent")
                        .HasColumnType("INTEGER");

                    b.Property<float>("RandomReplyChance")
                        .HasColumnType("REAL");

                    b.Property<int>("ResponseDelay")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("SkipNextBotMessage")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("StopBtnEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("SwipesEnabled")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.Guild", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("GuildMessagesFormat")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Guilds");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.HuntedUser", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<float>("Chance")
                        .HasColumnType("REAL");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("HuntedUsers");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.BlockedUser", b =>
                {
                    b.HasOne("CharacterAiDiscordBot.Models.Database.Guild", "Guild")
                        .WithMany("BlockedUsers")
                        .HasForeignKey("GuildId");

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.Channel", b =>
                {
                    b.HasOne("CharacterAiDiscordBot.Models.Database.Guild", "Guild")
                        .WithMany("Channels")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.HuntedUser", b =>
                {
                    b.HasOne("CharacterAiDiscordBot.Models.Database.Guild", "Guild")
                        .WithMany("HuntedUsers")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("CharacterAiDiscordBot.Models.Database.Guild", b =>
                {
                    b.Navigation("BlockedUsers");

                    b.Navigation("Channels");

                    b.Navigation("HuntedUsers");
                });
#pragma warning restore 612, 618
        }
    }
}
