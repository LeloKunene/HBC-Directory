using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HBCDirectory.Migrations
{
    /// <inheritdoc />
    public partial class AddCareGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CareGroupLeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CareGroupId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareGroupLeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareGroupLeaders_CareGroups_CareGroupId",
                        column: x => x.CareGroupId,
                        principalTable: "CareGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CareGroupLeaders_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CareGroupId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareGroupMembers_CareGroups_CareGroupId",
                        column: x => x.CareGroupId,
                        principalTable: "CareGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CareGroupMembers_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareGroupLeaders_CareGroupId_MemberId",
                table: "CareGroupLeaders",
                columns: new[] { "CareGroupId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareGroupLeaders_MemberId",
                table: "CareGroupLeaders",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CareGroupMembers_CareGroupId",
                table: "CareGroupMembers",
                column: "CareGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CareGroupMembers_MemberId",
                table: "CareGroupMembers",
                column: "MemberId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareGroupLeaders");

            migrationBuilder.DropTable(
                name: "CareGroupMembers");

            migrationBuilder.DropTable(
                name: "CareGroups");
        }
    }
}
