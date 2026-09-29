using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddLendingPaymentEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lending_payment_events",
                columns: table => new
                {
                    payment_event_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lending_record_id = table.Column<int>(type: "integer", nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount_paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    new_proposed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lending_payment_events", x => x.payment_event_id);
                    table.CheckConstraint("ck_payment_event_amount_positive", "amount_paid IS NULL OR amount_paid > 0");
                    table.CheckConstraint("ck_payment_event_has_content", "amount_paid IS NOT NULL OR new_proposed_date IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_lending_payment_events_lending_records_lending_record_id",
                        column: x => x.lending_record_id,
                        principalTable: "lending_records",
                        principalColumn: "lending_record_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lending_payment_events_lending_record_id",
                table: "lending_payment_events",
                column: "lending_record_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lending_payment_events");
        }
    }
}
