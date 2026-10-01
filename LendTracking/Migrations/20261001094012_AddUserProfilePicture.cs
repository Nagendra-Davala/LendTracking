using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilePicture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "profile_picture_url",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "profile_picture_url",
                table: "users");
        }
    }
}
