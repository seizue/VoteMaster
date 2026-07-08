using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddIncludeSignatureToTicketTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeSignature",
                table: "TicketTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeSignature",
                table: "TicketTemplates");
        }
    }
}
