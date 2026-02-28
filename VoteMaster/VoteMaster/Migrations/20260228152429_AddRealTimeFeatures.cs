using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddRealTimeFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableLiveVoteCount",
                table: "Polls",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePollNotifications",
                table: "Polls",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableLiveVoteCount",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "EnablePollNotifications",
                table: "Polls");
        }
    }
}
