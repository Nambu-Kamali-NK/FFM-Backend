using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFM.Workforce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofenceFieldsToAttendanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGeofenceValidated",
                table: "AttendanceRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "AttendanceRecords",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationRef",
                table: "AttendanceRecords",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "AttendanceRecords",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGeofenceValidated",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "LocationRef",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "AttendanceRecords");
        }
    }
}
