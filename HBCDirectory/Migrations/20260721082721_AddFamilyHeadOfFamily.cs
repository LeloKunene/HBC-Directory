using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HBCDirectory.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyHeadOfFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HeadOfFamilyId",
                table: "Families",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Families_HeadOfFamilyId",
                table: "Families",
                column: "HeadOfFamilyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Families_Members_HeadOfFamilyId",
                table: "Families",
                column: "HeadOfFamilyId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Families_Members_HeadOfFamilyId",
                table: "Families");

            migrationBuilder.DropIndex(
                name: "IX_Families_HeadOfFamilyId",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "HeadOfFamilyId",
                table: "Families");
        }
    }
}
