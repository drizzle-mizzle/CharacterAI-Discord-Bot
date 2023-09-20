using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterAiDiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class NullableGuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels");

            migrationBuilder.AlterColumn<ulong>(
                name: "GuildId",
                table: "Channels",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels");

            migrationBuilder.AlterColumn<ulong>(
                name: "GuildId",
                table: "Channels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
