using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddCapitalizeInterest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "capitalize_interest",
                table: "lending_payment_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capitalize_interest",
                table: "lending_payment_events");
        }
    }
}
