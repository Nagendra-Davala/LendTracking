using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_registrations",
                columns: table => new
                {
                    pending_registration_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_registrations", x => x.pending_registration_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    google_subject_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    group_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.group_id);
                    table.ForeignKey(
                        name: "FK_groups_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lending_records",
                columns: table => new
                {
                    lending_record_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_user_id = table.Column<int>(type: "integer", nullable: false),
                    counterparty_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    counterparty_user_id = table.Column<int>(type: "integer", nullable: true),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    annual_rate_percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    given_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_cleared = table.Column<bool>(type: "boolean", nullable: false),
                    cleared_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lending_records", x => x.lending_record_id);
                    table.CheckConstraint("ck_lending_cleared_date", "cleared_date IS NULL OR cleared_date >= given_date");
                    table.CheckConstraint("ck_lending_cleared_flag", "(is_cleared AND cleared_date IS NOT NULL) OR (NOT is_cleared AND cleared_date IS NULL)");
                    table.CheckConstraint("ck_lending_direction", "direction IN ('Lent', 'Borrowed')");
                    table.CheckConstraint("ck_lending_principal_positive", "principal > 0");
                    table.CheckConstraint("ck_lending_rate_not_negative", "annual_rate_percent >= 0");
                    table.ForeignKey(
                        name: "FK_lending_records_users_counterparty_user_id",
                        column: x => x.counterparty_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lending_records_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    expense_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    paid_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    expense_date = table.Column<DateOnly>(type: "date", nullable: false),
                    split_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.expense_id);
                    table.CheckConstraint("ck_expense_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_expense_split_type", "split_type IN ('equal', 'custom', 'percentage')");
                    table.ForeignKey(
                        name: "FK_expenses_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expenses_users_paid_by_user_id",
                        column: x => x.paid_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    group_member_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_members", x => x.group_member_id);
                    table.ForeignKey(
                        name: "FK_group_members_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "settlements",
                columns: table => new
                {
                    settlement_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    paid_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    paid_to_user_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    settled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlements", x => x.settlement_id);
                    table.CheckConstraint("ck_settlement_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_settlement_distinct_users", "paid_by_user_id <> paid_to_user_id");
                    table.ForeignKey(
                        name: "FK_settlements_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_settlements_users_paid_by_user_id",
                        column: x => x.paid_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_settlements_users_paid_to_user_id",
                        column: x => x.paid_to_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_shares",
                columns: table => new
                {
                    expense_share_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    share_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    share_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_shares", x => x.expense_share_id);
                    table.CheckConstraint("ck_share_amount_not_negative", "share_amount >= 0");
                    table.CheckConstraint("ck_share_percent_range", "share_percent IS NULL OR (share_percent >= 0 AND share_percent <= 100)");
                    table.ForeignKey(
                        name: "FK_expense_shares_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "expense_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expense_shares_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expense_shares_expense_id_user_id",
                table: "expense_shares",
                columns: new[] { "expense_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_shares_user_id",
                table: "expense_shares",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_expense_date",
                table: "expenses",
                column: "expense_date");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id",
                table: "expenses",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_paid_by_user_id",
                table: "expenses",
                column: "paid_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_group_id_user_id",
                table: "group_members",
                columns: new[] { "group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_members_user_id",
                table: "group_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_groups_created_by_user_id",
                table: "groups",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_lending_records_counterparty_user_id",
                table: "lending_records",
                column: "counterparty_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_lending_records_given_date",
                table: "lending_records",
                column: "given_date");

            migrationBuilder.CreateIndex(
                name: "IX_lending_records_owner_user_id",
                table: "lending_records",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_email",
                table: "pending_registrations",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_expires_at",
                table: "pending_registrations",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_token_hash",
                table: "pending_registrations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settlements_group_id",
                table: "settlements",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_paid_by_user_id",
                table: "settlements",
                column: "paid_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_paid_to_user_id",
                table: "settlements",
                column: "paid_to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_settled_date",
                table: "settlements",
                column: "settled_date");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_google_subject_id",
                table: "users",
                column: "google_subject_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_user_name",
                table: "users",
                column: "user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_shares");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "lending_records");

            migrationBuilder.DropTable(
                name: "pending_registrations");

            migrationBuilder.DropTable(
                name: "settlements");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
