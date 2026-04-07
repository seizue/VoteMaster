using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdminId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedByAdminId",
                table: "Users",
                column: "CreatedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_CreatedByAdminId",
                table: "Users",
                column: "CreatedByAdminId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_CreatedByAdminId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedByAdminId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                table: "Users");
        }
    }
}
