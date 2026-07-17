using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HBCDirectory.Migrations
{
    /// <inheritdoc />
    public partial class RolesAndStaffRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Members_Email",
                table: "Members");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ChurchOffice",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberType",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MemberStatus",
                table: "Members",
                type: "text",
                nullable: true);

            // DATA MIGRATION — map old Role values to the new fields.
            // Must run while "Role" still exists, before it's dropped below.
            migrationBuilder.Sql(@"
                UPDATE ""Members"" SET ""MemberStatus"" = 'Member';
                UPDATE ""Members"" SET ""ChurchOffice"" = 'Elder'  WHERE ""Role"" = 'Elder';
                UPDATE ""Members"" SET ""ChurchOffice"" = 'Deacon' WHERE ""Role"" = 'Deacon';
                UPDATE ""Members"" SET ""MemberType"" = 'Adult';
            ");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Members");

            migrationBuilder.AddColumn<string>(
                name: "AdditionalNotes",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyPhone",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleName = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRoles", x => x.Id);
                });

            // Seed default staff roles
            migrationBuilder.Sql(@"
                INSERT INTO ""StaffRoles"" (""RoleName"", ""DisplayOrder"") VALUES
                ('Campus Worker', 1),
                ('Secretary', 2),
                ('Pastoral Intern', 3);
            ");

            migrationBuilder.CreateTable(
                name: "StaffAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    StaffRoleId = table.Column<int>(type: "integer", nullable: false),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffAssignments_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffAssignments_StaffRoles_StaffRoleId",
                        column: x => x.StaffRoleId,
                        principalTable: "StaffRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAssignments_MemberId",
                table: "StaffAssignments",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAssignments_StaffRoleId",
                table: "StaffAssignments",
                column: "StaffRoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffAssignments");

            migrationBuilder.DropTable(
                name: "StaffRoles");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Members",
                type: "text",
                nullable: true);

            // Best-effort reverse data migration
            migrationBuilder.Sql(@"
                UPDATE ""Members"" SET ""Role"" = ""ChurchOffice"" WHERE ""ChurchOffice"" IS NOT NULL;
            ");

            migrationBuilder.DropColumn(
                name: "ChurchOffice",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MemberType",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MemberStatus",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "AdditionalNotes",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "FamilyPhone",
                table: "Families");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_Email",
                table: "Members",
                column: "Email",
                unique: true);
        }
    }
}