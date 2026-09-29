using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldExcessAsCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hold_excess_as_credit",
                table: "lending_payment_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hold_excess_as_credit",
                table: "lending_payment_events");
        }
    }
}
