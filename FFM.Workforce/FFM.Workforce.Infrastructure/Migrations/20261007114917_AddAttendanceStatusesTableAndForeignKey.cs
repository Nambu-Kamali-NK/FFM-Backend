using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FFM.Workforce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceStatusesTableAndForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AttendanceRecords",
                newName: "StatusId");

            migrationBuilder.Sql("""
UPDATE "AttendanceRecords" SET "StatusId" = CASE 
    WHEN "StatusId" = 'PRESENT' THEN '1'
    WHEN "StatusId" = 'ON_BREAK' THEN '2'
    WHEN "StatusId" = 'COMPLETED' THEN '3'
    WHEN "StatusId" = 'CORRECTED' THEN '4'
    WHEN "StatusId" = 'INCOMPLETE' THEN '5'
    ELSE '1'
END;
""");

            migrationBuilder.Sql("""
ALTER TABLE "AttendanceRecords" ALTER COLUMN "StatusId" TYPE integer USING "StatusId"::integer;
""");

            migrationBuilder.CreateTable(
                name: "AttendanceStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceStatuses", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AttendanceStatuses",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { 1, "PRESENT", "Employee is active and checked in" },
                    { 2, "ON_BREAK", "Employee is currently on an active break" },
                    { 3, "COMPLETED", "Attendance session completed and checked out" },
                    { 4, "CORRECTED", "Attendance record regularized or corrected by manager" },
                    { 5, "INCOMPLETE", "Attendance session incomplete or requires attention" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceStatuses");

            migrationBuilder.RenameColumn(
                name: "StatusId",
                table: "AttendanceRecords",
                newName: "Status");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AttendanceRecords",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
