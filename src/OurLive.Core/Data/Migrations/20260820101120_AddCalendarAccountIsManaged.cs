using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurLive.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarAccountIsManaged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManaged",
                table: "CalendarAccounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManaged",
                table: "CalendarAccounts");
        }
    }
}
