using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFM.Workforce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgUnitAndMakeUserIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                table: "Employees");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Employees",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "OrgUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnits", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "OrgUnits",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "IsDefault", "Name", "ParentId", "Status", "Type" },
                values: new object[] { new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Default Organization", null, "ACTIVE", "ORGANIZATION" });

            // Reassign all existing Employee rows to the stable default OrgUnit so that
            // the subsequent AddForeignKey does not fail on pre-existing data.
            migrationBuilder.Sql(
                """UPDATE "Employees" SET "OrgUnitId" = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890' WHERE "OrgUnitId" NOT IN (SELECT "Id" FROM "OrgUnits");""");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_OrgUnitId",
                table: "Employees",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_IsDefault_Unique",
                table: "OrgUnits",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_OrgUnits_OrgUnitId",
                table: "Employees",
                column: "OrgUnitId",
                principalTable: "OrgUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_OrgUnits_OrgUnitId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "OrgUnits");

            migrationBuilder.DropIndex(
                name: "IX_Employees_OrgUnitId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                table: "Employees");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);
        }
    }
}
