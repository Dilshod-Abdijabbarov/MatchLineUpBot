using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LineUpBot.Migrations
{
    /// <inheritdoc />
    public partial class update_telegramGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "GroupUsers");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "BotUsers");

            migrationBuilder.RenameColumn(
                name: "ChatId",
                table: "BotUsers",
                newName: "TelegramUserChatId");

            migrationBuilder.AddColumn<string>(
                name: "PollId",
                table: "Surveys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TelegramGroupId",
                table: "Surveys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserRole",
                table: "BotUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SurveyBotUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    BotUserId = table.Column<int>(type: "integer", nullable: false),
                    SurveyId = table.Column<int>(type: "integer", nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyBotUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyBotUsers_BotUsers_BotUserId",
                        column: x => x.BotUserId,
                        principalTable: "BotUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SurveyBotUsers_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TelegramGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TelegramGroupChatId = table.Column<long>(type: "bigint", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BotUserTelegramGroup",
                columns: table => new
                {
                    BotUsersId = table.Column<int>(type: "integer", nullable: false),
                    TelegramGroupsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotUserTelegramGroup", x => new { x.BotUsersId, x.TelegramGroupsId });
                    table.ForeignKey(
                        name: "FK_BotUserTelegramGroup_BotUsers_BotUsersId",
                        column: x => x.BotUsersId,
                        principalTable: "BotUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BotUserTelegramGroup_TelegramGroups_TelegramGroupsId",
                        column: x => x.TelegramGroupsId,
                        principalTable: "TelegramGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Surveys_TelegramGroupId",
                table: "Surveys",
                column: "TelegramGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BotUserTelegramGroup_TelegramGroupsId",
                table: "BotUserTelegramGroup",
                column: "TelegramGroupsId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyBotUsers_BotUserId",
                table: "SurveyBotUsers",
                column: "BotUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyBotUsers_SurveyId",
                table: "SurveyBotUsers",
                column: "SurveyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Surveys_TelegramGroups_TelegramGroupId",
                table: "Surveys",
                column: "TelegramGroupId",
                principalTable: "TelegramGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Surveys_TelegramGroups_TelegramGroupId",
                table: "Surveys");

            migrationBuilder.DropTable(
                name: "BotUserTelegramGroup");

            migrationBuilder.DropTable(
                name: "SurveyBotUsers");

            migrationBuilder.DropTable(
                name: "TelegramGroups");

            migrationBuilder.DropIndex(
                name: "IX_Surveys_TelegramGroupId",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "PollId",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "TelegramGroupId",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "UserRole",
                table: "BotUsers");

            migrationBuilder.RenameColumn(
                name: "TelegramUserChatId",
                table: "BotUsers",
                newName: "ChatId");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "BotUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurveyId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SurveyId",
                table: "Groups",
                column: "SurveyId");
        }
    }
}
