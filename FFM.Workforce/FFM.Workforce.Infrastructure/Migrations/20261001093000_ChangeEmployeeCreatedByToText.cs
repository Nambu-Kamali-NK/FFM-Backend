using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFM.Workforce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEmployeeCreatedByToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Employees"" ALTER COLUMN ""CreatedBy"" TYPE text USING ""CreatedBy""::text;

                -- Update existing integer values to display names
                UPDATE ""Employees"" SET ""CreatedBy"" = 'System Admin' WHERE ""CreatedBy"" = '1' OR ""CreatedBy"" = '12';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Employees"" SET ""CreatedBy"" = '1' WHERE ""CreatedBy"" = 'System Admin';
                ALTER TABLE ""Employees"" ALTER COLUMN ""CreatedBy"" TYPE integer USING (""CreatedBy""::integer);
            ");
        }
    }
}
