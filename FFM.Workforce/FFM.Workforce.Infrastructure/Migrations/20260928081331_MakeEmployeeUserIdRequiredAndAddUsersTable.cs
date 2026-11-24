using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFM.Workforce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeEmployeeUserIdRequiredAndAddUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                table: "Employees");

            // Handle existing Employee records where UserId is NULL before making it NOT NULL
            migrationBuilder.Sql(
                "UPDATE \"Employees\" SET \"UserId\" = gen_random_uuid() WHERE \"UserId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Users table already managed by Identity context
            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Users table already managed by Identity context
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

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId");
        }
    }
}
