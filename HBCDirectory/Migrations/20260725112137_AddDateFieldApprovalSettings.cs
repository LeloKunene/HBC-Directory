using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HBCDirectory.Migrations
{
    /// <inheritdoc />
    public partial class AddDateFieldApprovalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovalForAnniversary",
                table: "ApprovalSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovalForBirthdate",
                table: "ApprovalSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovalForDateJoined",
                table: "ApprovalSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireApprovalForAnniversary",
                table: "ApprovalSettings");

            migrationBuilder.DropColumn(
                name: "RequireApprovalForBirthdate",
                table: "ApprovalSettings");

            migrationBuilder.DropColumn(
                name: "RequireApprovalForDateJoined",
                table: "ApprovalSettings");
        }
    }
}
