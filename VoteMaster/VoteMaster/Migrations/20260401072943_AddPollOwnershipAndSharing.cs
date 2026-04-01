using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddPollOwnershipAndSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Polls",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PollShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PollId = table.Column<int>(type: "int", nullable: false),
                    SharedWithUserId = table.Column<int>(type: "int", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollShares_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollShares_Users_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Polls_OwnerId",
                table: "Polls",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PollShares_PollId_SharedWithUserId",
                table: "PollShares",
                columns: new[] { "PollId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollShares_SharedWithUserId",
                table: "PollShares",
                column: "SharedWithUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Polls_Users_OwnerId",
                table: "Polls",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Polls_Users_OwnerId",
                table: "Polls");

            migrationBuilder.DropTable(
                name: "PollShares");

            migrationBuilder.DropIndex(
                name: "IX_Polls_OwnerId",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Polls");
        }
    }
}
