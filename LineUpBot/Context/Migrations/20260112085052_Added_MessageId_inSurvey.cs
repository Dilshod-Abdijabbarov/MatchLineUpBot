using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LineUpBot.Migrations
{
    /// <inheritdoc />
    public partial class Added_MessageId_inSurvey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TelegramUserId",
                table: "GroupUsers",
                newName: "ChatId");

            migrationBuilder.AddColumn<int>(
                name: "MessageId",
                table: "Surveys",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "Surveys");

            migrationBuilder.RenameColumn(
                name: "ChatId",
                table: "GroupUsers",
                newName: "TelegramUserId");
        }
    }
}
