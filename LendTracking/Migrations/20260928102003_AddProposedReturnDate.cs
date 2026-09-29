using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddProposedReturnDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "proposed_return_date",
                table: "lending_records",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proposed_return_date",
                table: "lending_records");
        }
    }
}
