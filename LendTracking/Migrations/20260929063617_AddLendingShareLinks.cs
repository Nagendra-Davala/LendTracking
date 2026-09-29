using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddLendingShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lending_share_links",
                columns: table => new
                {
                    share_link_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lending_record_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lending_share_links", x => x.share_link_id);
                    table.ForeignKey(
                        name: "FK_lending_share_links_lending_records_lending_record_id",
                        column: x => x.lending_record_id,
                        principalTable: "lending_records",
                        principalColumn: "lending_record_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lending_share_links_lending_record_id",
                table: "lending_share_links",
                column: "lending_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_lending_share_links_token",
                table: "lending_share_links",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lending_share_links");
        }
    }
}
