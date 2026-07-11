using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddPlainPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlainPassword",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlainPassword",
                table: "Users");
        }
    }
}
