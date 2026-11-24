using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFM.Identity.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserIdToNumeric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create persistent mapping table to preserve UUID <-> integer mapping
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""_UserUuidMigrationMap"" (
                    ""OldUuid"" uuid PRIMARY KEY,
                    ""NewIntId"" integer NOT NULL UNIQUE
                );
            ");

            // 2. Populate mapping table from existing Users (preserving any already mapped)
            migrationBuilder.Sql(@"
                INSERT INTO ""_UserUuidMigrationMap"" (""OldUuid"", ""NewIntId"")
                SELECT u.""Id"", COALESCE(m.max_id, 0) + ROW_NUMBER() OVER (ORDER BY u.""UsernameOrEmail"")::integer
                FROM ""Users"" u
                CROSS JOIN (SELECT COALESCE(MAX(""NewIntId""), 0) AS max_id FROM ""_UserUuidMigrationMap"") m
                WHERE NOT EXISTS (SELECT 1 FROM ""_UserUuidMigrationMap"" map WHERE map.""OldUuid"" = u.""Id"")
                ON CONFLICT (""OldUuid"") DO NOTHING;
            ");

            // 3. Validation: Verify that EVERY existing UUID reference in dependent tables has a corresponding mapping
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    unmapped_count integer;
                BEGIN
                    SELECT COUNT(*) INTO unmapped_count
                    FROM ""RefreshTokens"" rt
                    WHERE NOT EXISTS (SELECT 1 FROM ""_UserUuidMigrationMap"" m WHERE m.""OldUuid"" = rt.""UserId"");
                    IF unmapped_count > 0 THEN
                        RAISE EXCEPTION 'Validation failed: % unmapped UserId references in RefreshTokens', unmapped_count;
                    END IF;

                    SELECT COUNT(*) INTO unmapped_count
                    FROM ""UserDevices"" ud
                    WHERE NOT EXISTS (SELECT 1 FROM ""_UserUuidMigrationMap"" m WHERE m.""OldUuid"" = ud.""UserId"");
                    IF unmapped_count > 0 THEN
                        RAISE EXCEPTION 'Validation failed: % unmapped UserId references in UserDevices', unmapped_count;
                    END IF;

                    SELECT COUNT(*) INTO unmapped_count
                    FROM ""MfaChallenges"" mc
                    WHERE NOT EXISTS (SELECT 1 FROM ""_UserUuidMigrationMap"" m WHERE m.""OldUuid"" = mc.""UserId"");
                    IF unmapped_count > 0 THEN
                        RAISE EXCEPTION 'Validation failed: % unmapped UserId references in MfaChallenges', unmapped_count;
                    END IF;

                    SELECT COUNT(*) INTO unmapped_count
                    FROM ""AuthAuditLogs"" aal
                    WHERE NOT EXISTS (SELECT 1 FROM ""_UserUuidMigrationMap"" m WHERE m.""OldUuid"" = aal.""UserId"");
                    IF unmapped_count > 0 THEN
                        RAISE EXCEPTION 'Validation failed: % unmapped UserId references in AuthAuditLogs', unmapped_count;
                    END IF;
                END $$;
            ");

            // 4. Drop foreign key constraints on dependent tables
            migrationBuilder.Sql(@"
                ALTER TABLE ""RefreshTokens"" DROP CONSTRAINT IF EXISTS ""FK_RefreshTokens_Users_UserId"";
                ALTER TABLE ""UserDevices"" DROP CONSTRAINT IF EXISTS ""FK_UserDevices_Users_UserId"";
                ALTER TABLE ""MfaChallenges"" DROP CONSTRAINT IF EXISTS ""FK_MfaChallenges_Users_UserId"";
            ");

            // 5. Migrate dependent table foreign key columns using the explicit mapping
            migrationBuilder.Sql(@"
                -- RefreshTokens
                ALTER TABLE ""RefreshTokens"" ADD COLUMN ""NewUserId"" integer;
                UPDATE ""RefreshTokens"" rt SET ""NewUserId"" = m.""NewIntId""
                FROM ""_UserUuidMigrationMap"" m WHERE rt.""UserId"" = m.""OldUuid"";
                ALTER TABLE ""RefreshTokens"" ALTER COLUMN ""NewUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_RefreshTokens_UserId"";
                ALTER TABLE ""RefreshTokens"" DROP COLUMN ""UserId"";
                ALTER TABLE ""RefreshTokens"" RENAME COLUMN ""NewUserId"" TO ""UserId"";
                CREATE INDEX ""IX_RefreshTokens_UserId"" ON ""RefreshTokens"" (""UserId"");

                -- UserDevices
                ALTER TABLE ""UserDevices"" ADD COLUMN ""NewUserId"" integer;
                UPDATE ""UserDevices"" ud SET ""NewUserId"" = m.""NewIntId""
                FROM ""_UserUuidMigrationMap"" m WHERE ud.""UserId"" = m.""OldUuid"";
                ALTER TABLE ""UserDevices"" ALTER COLUMN ""NewUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_UserDevices_UserId"";
                ALTER TABLE ""UserDevices"" DROP COLUMN ""UserId"";
                ALTER TABLE ""UserDevices"" RENAME COLUMN ""NewUserId"" TO ""UserId"";
                CREATE INDEX ""IX_UserDevices_UserId"" ON ""UserDevices"" (""UserId"");

                -- MfaChallenges
                ALTER TABLE ""MfaChallenges"" ADD COLUMN ""NewUserId"" integer;
                UPDATE ""MfaChallenges"" mc SET ""NewUserId"" = m.""NewIntId""
                FROM ""_UserUuidMigrationMap"" m WHERE mc.""UserId"" = m.""OldUuid"";
                ALTER TABLE ""MfaChallenges"" ALTER COLUMN ""NewUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_MfaChallenges_UserId"";
                ALTER TABLE ""MfaChallenges"" DROP COLUMN ""UserId"";
                ALTER TABLE ""MfaChallenges"" RENAME COLUMN ""NewUserId"" TO ""UserId"";
                CREATE INDEX ""IX_MfaChallenges_UserId"" ON ""MfaChallenges"" (""UserId"");

                -- AuthAuditLogs
                ALTER TABLE ""AuthAuditLogs"" ADD COLUMN ""NewUserId"" integer;
                UPDATE ""AuthAuditLogs"" aal SET ""NewUserId"" = m.""NewIntId""
                FROM ""_UserUuidMigrationMap"" m WHERE aal.""UserId"" = m.""OldUuid"";
                ALTER TABLE ""AuthAuditLogs"" ALTER COLUMN ""NewUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_AuthAuditLogs_UserId"";
                ALTER TABLE ""AuthAuditLogs"" DROP COLUMN ""UserId"";
                ALTER TABLE ""AuthAuditLogs"" RENAME COLUMN ""NewUserId"" TO ""UserId"";
                CREATE INDEX ""IX_AuthAuditLogs_UserId"" ON ""AuthAuditLogs"" (""UserId"");
            ");

            // 6. Migrate Users.Id to integer with PostgreSQL GENERATED BY DEFAULT AS IDENTITY
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" DROP CONSTRAINT ""PK_Users"";
                ALTER TABLE ""Users"" ADD COLUMN ""NewId"" integer;
                UPDATE ""Users"" u SET ""NewId"" = m.""NewIntId""
                FROM ""_UserUuidMigrationMap"" m WHERE u.""Id"" = m.""OldUuid"";
                ALTER TABLE ""Users"" ALTER COLUMN ""NewId"" SET NOT NULL;
                ALTER TABLE ""Users"" DROP COLUMN ""Id"";
                ALTER TABLE ""Users"" RENAME COLUMN ""NewId"" TO ""Id"";
                ALTER TABLE ""Users"" ADD CONSTRAINT ""PK_Users"" PRIMARY KEY (""Id"");
                ALTER TABLE ""Users"" ALTER COLUMN ""Id"" ADD GENERATED BY DEFAULT AS IDENTITY;

                -- Ensure identity sequence starts after the highest existing integer ID
                DO $$
                DECLARE
                    max_id integer;
                BEGIN
                    SELECT COALESCE(MAX(""Id""), 0) INTO max_id FROM ""Users"";
                    EXECUTE 'ALTER TABLE ""Users"" ALTER COLUMN ""Id"" RESTART WITH ' || (max_id + 1);
                END $$;
            ");

            // 7. Re-establish foreign keys referencing the new integer Users.Id
            migrationBuilder.Sql(@"
                ALTER TABLE ""RefreshTokens"" ADD CONSTRAINT ""FK_RefreshTokens_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""UserDevices"" ADD CONSTRAINT ""FK_UserDevices_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""MfaChallenges"" ADD CONSTRAINT ""FK_MfaChallenges_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Drop foreign keys referencing integer Users.Id
            migrationBuilder.Sql(@"
                ALTER TABLE ""RefreshTokens"" DROP CONSTRAINT IF EXISTS ""FK_RefreshTokens_Users_UserId"";
                ALTER TABLE ""UserDevices"" DROP CONSTRAINT IF EXISTS ""FK_UserDevices_Users_UserId"";
                ALTER TABLE ""MfaChallenges"" DROP CONSTRAINT IF EXISTS ""FK_MfaChallenges_Users_UserId"";
            ");

            // 2. Rollback dependent table foreign key columns back to UUID using the mapping table
            migrationBuilder.Sql(@"
                -- RefreshTokens
                ALTER TABLE ""RefreshTokens"" ADD COLUMN ""OldUserId"" uuid;
                UPDATE ""RefreshTokens"" rt SET ""OldUserId"" = m.""OldUuid""
                FROM ""_UserUuidMigrationMap"" m WHERE rt.""UserId"" = m.""NewIntId"";
                ALTER TABLE ""RefreshTokens"" ALTER COLUMN ""OldUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_RefreshTokens_UserId"";
                ALTER TABLE ""RefreshTokens"" DROP COLUMN ""UserId"";
                ALTER TABLE ""RefreshTokens"" RENAME COLUMN ""OldUserId"" TO ""UserId"";
                CREATE INDEX ""IX_RefreshTokens_UserId"" ON ""RefreshTokens"" (""UserId"");

                -- UserDevices
                ALTER TABLE ""UserDevices"" ADD COLUMN ""OldUserId"" uuid;
                UPDATE ""UserDevices"" ud SET ""OldUserId"" = m.""OldUuid""
                FROM ""_UserUuidMigrationMap"" m WHERE ud.""UserId"" = m.""NewIntId"";
                ALTER TABLE ""UserDevices"" ALTER COLUMN ""OldUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_UserDevices_UserId"";
                ALTER TABLE ""UserDevices"" DROP COLUMN ""UserId"";
                ALTER TABLE ""UserDevices"" RENAME COLUMN ""OldUserId"" TO ""UserId"";
                CREATE INDEX ""IX_UserDevices_UserId"" ON ""UserDevices"" (""UserId"");

                -- MfaChallenges
                ALTER TABLE ""MfaChallenges"" ADD COLUMN ""OldUserId"" uuid;
                UPDATE ""MfaChallenges"" mc SET ""OldUserId"" = m.""OldUuid""
                FROM ""_UserUuidMigrationMap"" m WHERE mc.""UserId"" = m.""NewIntId"";
                ALTER TABLE ""MfaChallenges"" ALTER COLUMN ""OldUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_MfaChallenges_UserId"";
                ALTER TABLE ""MfaChallenges"" DROP COLUMN ""UserId"";
                ALTER TABLE ""MfaChallenges"" RENAME COLUMN ""OldUserId"" TO ""UserId"";
                CREATE INDEX ""IX_MfaChallenges_UserId"" ON ""MfaChallenges"" (""UserId"");

                -- AuthAuditLogs
                ALTER TABLE ""AuthAuditLogs"" ADD COLUMN ""OldUserId"" uuid;
                UPDATE ""AuthAuditLogs"" aal SET ""OldUserId"" = m.""OldUuid""
                FROM ""_UserUuidMigrationMap"" m WHERE aal.""UserId"" = m.""NewIntId"";
                ALTER TABLE ""AuthAuditLogs"" ALTER COLUMN ""OldUserId"" SET NOT NULL;
                DROP INDEX IF EXISTS ""IX_AuthAuditLogs_UserId"";
                ALTER TABLE ""AuthAuditLogs"" DROP COLUMN ""UserId"";
                ALTER TABLE ""AuthAuditLogs"" RENAME COLUMN ""OldUserId"" TO ""UserId"";
                CREATE INDEX ""IX_AuthAuditLogs_UserId"" ON ""AuthAuditLogs"" (""UserId"");
            ");

            // 3. Rollback Users.Id back to UUID using the mapping table
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" ALTER COLUMN ""Id"" DROP IDENTITY IF EXISTS;
                ALTER TABLE ""Users"" DROP CONSTRAINT ""PK_Users"";
                ALTER TABLE ""Users"" ADD COLUMN ""OldId"" uuid;
                UPDATE ""Users"" u SET ""OldId"" = m.""OldUuid""
                FROM ""_UserUuidMigrationMap"" m WHERE u.""Id"" = m.""NewIntId"";
                ALTER TABLE ""Users"" ALTER COLUMN ""OldId"" SET NOT NULL;
                ALTER TABLE ""Users"" DROP COLUMN ""Id"";
                ALTER TABLE ""Users"" RENAME COLUMN ""OldId"" TO ""Id"";
                ALTER TABLE ""Users"" ADD CONSTRAINT ""PK_Users"" PRIMARY KEY (""Id"");
            ");

            // 4. Re-establish foreign keys referencing UUID Users.Id
            migrationBuilder.Sql(@"
                ALTER TABLE ""RefreshTokens"" ADD CONSTRAINT ""FK_RefreshTokens_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""UserDevices"" ADD CONSTRAINT ""FK_UserDevices_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE;

                ALTER TABLE ""MfaChallenges"" ADD CONSTRAINT ""FK_MfaChallenges_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE;
            ");
        }
    }
}
