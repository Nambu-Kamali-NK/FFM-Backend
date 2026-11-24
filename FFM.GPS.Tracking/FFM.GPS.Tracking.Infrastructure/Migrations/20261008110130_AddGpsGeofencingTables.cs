using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FFM.GPS.Tracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGpsGeofencingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ffmgps");

            migrationBuilder.CreateTable(
                name: "Geofences",
                schema: "ffmgps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LocationRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CenterLatitude = table.Column<double>(type: "double precision", nullable: true),
                    CenterLongitude = table.Column<double>(type: "double precision", nullable: true),
                    RadiusMeters = table.Column<double>(type: "double precision", nullable: true),
                    BufferMeters = table.Column<double>(type: "double precision", nullable: false, defaultValue: 15.0),
                    PolygonCoordinatesJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Geofences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeGeofences",
                schema: "ffmgps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    GeofenceId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeGeofences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeGeofences_Geofences_GeofenceId",
                        column: x => x.GeofenceId,
                        principalSchema: "ffmgps",
                        principalTable: "Geofences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGeofences_EmployeeId_GeofenceId",
                schema: "ffmgps",
                table: "EmployeeGeofences",
                columns: new[] { "EmployeeId", "GeofenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGeofences_GeofenceId",
                schema: "ffmgps",
                table: "EmployeeGeofences",
                column: "GeofenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Geofences_LocationRef",
                schema: "ffmgps",
                table: "Geofences",
                column: "LocationRef",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeGeofences",
                schema: "ffmgps");

            migrationBuilder.DropTable(
                name: "Geofences",
                schema: "ffmgps");
        }
    }
}
