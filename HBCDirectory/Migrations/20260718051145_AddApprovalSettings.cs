using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HBCDirectory.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequireApprovalForName = table.Column<bool>(type: "boolean", nullable: false),
                    RequireApprovalForPhone = table.Column<bool>(type: "boolean", nullable: false),
                    RequireApprovalForPrivacy = table.Column<bool>(type: "boolean", nullable: false),
                    RequireApprovalForPhoto = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSettings", x => x.Id);
                });

                // Seed default approval settings — only photo requires approval by default
                migrationBuilder.Sql(@"
                    INSERT INTO ""ApprovalSettings"" (""Id"", ""RequireApprovalForName"", ""RequireApprovalForPhone"", ""RequireApprovalForPrivacy"", ""RequireApprovalForPhoto"")
                    VALUES (1, false, false, false, true);
                ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalSettings");
        }
    }
}
