using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class FixPaymentEventContentConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payment_event_has_content",
                table: "lending_payment_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payment_event_has_content",
                table: "lending_payment_events",
                sql: "amount_paid IS NOT NULL OR new_proposed_date IS NOT NULL OR apply_held_credit OR capitalize_interest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payment_event_has_content",
                table: "lending_payment_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payment_event_has_content",
                table: "lending_payment_events",
                sql: "amount_paid IS NOT NULL OR new_proposed_date IS NOT NULL");
        }
    }
}
