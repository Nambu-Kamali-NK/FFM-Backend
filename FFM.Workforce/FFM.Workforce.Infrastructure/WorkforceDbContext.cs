using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Infrastructure;

public class WorkforceDbContext : DbContext
{
    // Stable Id for the default OrgUnit seed row.
    // This must never change once the migration is applied — it is the Id that
    // every auto-assigned employee will reference.
    public static readonly Guid DefaultOrgUnitId =
        Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    // Stable "system" actor used for the seed row's CreatedBy column.
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public WorkforceDbContext(DbContextOptions<WorkforceDbContext> options)
        : base(options) { }

    public DbSet<FFM.Identity.Access.Domain.Entities.User> Users => Set<FFM.Identity.Access.Domain.Entities.User>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<BreakRecord> BreakRecords => Set<BreakRecord>();
    public DbSet<AttendanceStatusLookup> AttendanceStatuses => Set<AttendanceStatusLookup>();
    public DbSet<AttendanceAuditLog> AttendanceAuditLogs => Set<AttendanceAuditLog>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<PermissionRequest> PermissionRequests => Set<PermissionRequest>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<RosterAssignment> RosterAssignments => Set<RosterAssignment>();
    public DbSet<FieldTaskEntity> Tasks => Set<FieldTaskEntity>();
    public DbSet<ClientSiteEntity> ClientSites => Set<ClientSiteEntity>();
    public DbSet<FFM.Identity.Access.Domain.Entities.UserDevice> UserDevices => Set<FFM.Identity.Access.Domain.Entities.UserDevice>();
    public DbSet<VisitEntity> Visits => Set<VisitEntity>();
    public DbSet<VisitEvidenceEntity> VisitEvidences => Set<VisitEvidenceEntity>();
    public DbSet<EvidenceTypeEntity> EvidenceTypes => Set<EvidenceTypeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── AttendanceStatusLookup ──────────────────────────────────────────
        modelBuilder.Entity<AttendanceStatusLookup>(entity =>
        {
            entity.ToTable("AttendanceStatuses");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();
            entity.Property(s => s.Code).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Description).HasMaxLength(250);

            entity.HasData(
                new AttendanceStatusLookup(1, "PRESENT", "Employee is active and checked in"),
                new AttendanceStatusLookup(2, "ON_BREAK", "Employee is currently on an active break"),
                new AttendanceStatusLookup(3, "COMPLETED", "Attendance session completed and checked out"),
                new AttendanceStatusLookup(4, "CORRECTED", "Attendance record regularized or corrected by manager"),
                new AttendanceStatusLookup(5, "INCOMPLETE", "Attendance session incomplete or requires attention")
            );
        });

        // ── AttendanceRecord ────────────────────────────────────────────────
        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedOnAdd();
            entity.Property(a => a.EmployeeId).IsRequired();
            entity.Property(a => a.WorkDate).IsRequired();
            entity.Property(a => a.CheckInTime).IsRequired();
            entity.Property(a => a.Status)
                .HasConversion<int>()
                .HasColumnName("StatusId")
                .IsRequired();

            entity.Property(a => a.Latitude).IsRequired(false);
            entity.Property(a => a.Longitude).IsRequired(false);
            entity.Property(a => a.IsGeofenceValidated).HasDefaultValue(false);
            entity.Property(a => a.LocationRef).HasMaxLength(255).IsRequired(false);

            entity.HasMany(a => a.Breaks)
                .WithOne()
                .HasForeignKey(b => b.AttendanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for fast lookup by employee and workdate
            entity.HasIndex(a => new { a.EmployeeId, a.WorkDate })
                .HasDatabaseName("IX_AttendanceRecords_Employee_WorkDate");

            // Enforce at most one active open check-in session per employee at any given time
            entity.HasIndex(a => a.EmployeeId)
                .HasFilter("\"CheckOutTime\" IS NULL")
                .IsUnique()
                .HasDatabaseName("IX_AttendanceRecords_Employee_ActiveSession_Unique");
        });

        // ── BreakRecord ─────────────────────────────────────────────────────
        modelBuilder.Entity<BreakRecord>(entity =>
        {
            entity.ToTable("BreakRecords");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedOnAdd();
            entity.Property(b => b.AttendanceId).IsRequired();
            entity.Property(b => b.BreakType).IsRequired();
            entity.Property(b => b.BreakStartTime).IsRequired();
        });

        // ── AttendanceAuditLog ──────────────────────────────────────────────
        modelBuilder.Entity<AttendanceAuditLog>(entity =>
        {
            entity.ToTable("AttendanceAuditLogs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedOnAdd();
            entity.Property(a => a.AttendanceId).IsRequired();
            entity.Property(a => a.ActorUserId).IsRequired();
            entity.Property(a => a.Action).IsRequired();
            entity.Property(a => a.Reason).IsRequired();
            entity.Property(a => a.PreviousValues).HasColumnType("jsonb");
            entity.Property(a => a.NewValues).HasColumnType("jsonb");
        });
        // ── Role ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedOnAdd();
            entity.Property(r => r.Name).IsRequired();
            entity.Property(r => r.Status).IsRequired();
        });

        // ── User (Identity) ─────────────────────────────────────────────────
        modelBuilder.Entity<FFM.Identity.Access.Domain.Entities.User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedOnAdd();
            entity.Property(u => u.UsernameOrEmail).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Status);
            entity.Property(u => u.Role).HasConversion<string>();
            entity.Ignore(u => u.Devices);
            entity.Ignore(u => u.RefreshTokens);
            entity.Ignore(u => u.PasswordResetTokens);
        });

        // ── OrgUnit ─────────────────────────────────────────────────────────
        modelBuilder.Entity<OrgUnit>(entity =>
        {
            entity.ToTable("OrgUnits");
            entity.HasKey(o => o.Id);

            entity.Property(o => o.Name).IsRequired();
            entity.Property(o => o.Type).IsRequired();
            entity.Property(o => o.Status).IsRequired();
            entity.Property(o => o.CreatedAt).IsRequired();
            entity.Property(o => o.CreatedBy).IsRequired();
            entity.Property(o => o.IsDefault).IsRequired();

            // Index to make the default-lookup fast and enforce at most one default.
            entity.HasIndex(o => o.IsDefault)
                .HasFilter("\"IsDefault\" = TRUE")
                .IsUnique()
                .HasDatabaseName("IX_OrgUnits_IsDefault_Unique");

            // Seed the one stable default OrgUnit.
            // HasData requires all properties to be set; CreatedAt must be a fixed value.
            entity.HasData(new
            {
                Id = DefaultOrgUnitId,
                Name = "Default Organization",
                Type = "ORGANIZATION",
                Status = "ACTIVE",
                IsDefault = true,
                CreatedBy = SystemActorId,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ParentId = (Guid?)null
            });
        });

        // ── Employee ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasIndex(e => e.EmployeeCode).IsUnique();

            // UserId is required and unique (one-to-one mapping with Identity User)
            entity.HasIndex(e => e.UserId).IsUnique();

            entity.Property(e => e.EmployeeCode).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();

            // FK → Role
            entity.HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // FK → OrgUnit
            entity.HasOne<OrgUnit>()
                .WithMany()
                .HasForeignKey(e => e.OrgUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AuditLog ─────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.EntityType).IsRequired();
            entity.Property(a => a.Action).IsRequired();
            entity.Property(a => a.CreatedAt).IsRequired();
        });

        // ── OutboxMessage ────────────────────────────────────────────────────
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.EventType).IsRequired();
            entity.Property(o => o.Payload).IsRequired();
            entity.Property(o => o.CreatedAt).IsRequired();
            entity.HasIndex(o => o.Processed)
                .HasFilter("\"Processed\" = FALSE");
        });

        // ── LeaveType ────────────────────────────────────────────────────────
        modelBuilder.Entity<LeaveType>(entity =>
        {
            entity.ToTable("LeaveTypes");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).ValueGeneratedOnAdd();
            entity.Property(t => t.Code).IsRequired().HasMaxLength(50);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
            entity.Property(t => t.DefaultDaysPerYear).HasPrecision(5, 2).IsRequired();
            entity.Property(t => t.IsPaid).IsRequired();
            entity.Property(t => t.AllowNegativeBalance).IsRequired();
            entity.Property(t => t.IsActive).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();

            entity.HasIndex(t => t.Code).IsUnique().HasDatabaseName("IX_LeaveTypes_Code_Unique");

            entity.HasData(
                new LeaveType(1, "CASUAL", "Casual Leave", 12.0m, true, false, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                new LeaveType(2, "SICK", "Sick Leave", 10.0m, true, false, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                new LeaveType(3, "EARNED", "Earned / Annual Leave", 15.0m, true, false, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                new LeaveType(4, "UNPAID", "Loss of Pay / Unpaid Leave", 0.0m, false, true, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            );
        });

        // ── LeaveBalance ─────────────────────────────────────────────────────
        modelBuilder.Entity<LeaveBalance>(entity =>
        {
            entity.ToTable("LeaveBalances");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedOnAdd();
            entity.Property(b => b.EmployeeId).IsRequired();
            entity.Property(b => b.LeaveTypeId).IsRequired();
            entity.Property(b => b.Year).IsRequired();
            entity.Property(b => b.TotalDays).HasPrecision(5, 2).IsRequired();
            entity.Property(b => b.UsedDays).HasPrecision(5, 2).IsRequired();
            entity.Property(b => b.UpdatedAt).IsRequired();

            entity.Ignore(b => b.AvailableDays);

            entity.HasOne(b => b.LeaveType)
                .WithMany()
                .HasForeignKey(b => b.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(b => b.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => new { b.EmployeeId, b.LeaveTypeId, b.Year })
                .IsUnique()
                .HasDatabaseName("IX_LeaveBalances_Employee_LeaveType_Year_Unique");
        });

        // ── LeaveRequest ─────────────────────────────────────────────────────
        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequests");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedOnAdd();
            entity.Property(r => r.EmployeeId).IsRequired();
            entity.Property(r => r.LeaveTypeId).IsRequired();
            entity.Property(r => r.StartDate).IsRequired();
            entity.Property(r => r.EndDate).IsRequired();
            entity.Property(r => r.DaysCount).HasPrecision(5, 2).IsRequired();
            entity.Property(r => r.IsHalfDay).IsRequired();
            entity.Property(r => r.Reason).IsRequired().HasMaxLength(500);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(r => r.ApproverId).IsRequired(false);
            entity.Property(r => r.DecisionComments).HasMaxLength(500);
            entity.Property(r => r.DecidedAt).IsRequired(false);
            entity.Property(r => r.CreatedAt).IsRequired();

            entity.HasOne(r => r.LeaveType)
                .WithMany()
                .HasForeignKey(r => r.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => new { r.EmployeeId, r.Status })
                .HasDatabaseName("IX_LeaveRequests_Employee_Status");

            entity.HasIndex(r => new { r.StartDate, r.EndDate })
                .HasDatabaseName("IX_LeaveRequests_Dates");
        });

        // ── PermissionRequest ────────────────────────────────────────────────
        modelBuilder.Entity<PermissionRequest>(entity =>
        {
            entity.ToTable("PermissionRequests");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.EmployeeId).IsRequired();
            entity.Property(p => p.PermissionDate).IsRequired();
            entity.Property(p => p.StartTime).IsRequired();
            entity.Property(p => p.EndTime).IsRequired();
            entity.Property(p => p.DurationHours).HasPrecision(4, 2).IsRequired();
            entity.Property(p => p.Reason).IsRequired().HasMaxLength(500);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(p => p.ApproverId).IsRequired(false);
            entity.Property(p => p.DecisionComments).HasMaxLength(500);
            entity.Property(p => p.DecidedAt).IsRequired(false);
            entity.Property(p => p.CreatedAt).IsRequired();

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.EmployeeId, p.PermissionDate })
                .HasDatabaseName("IX_PermissionRequests_Employee_Date");
        });

        // ── Shifts ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.ToTable("Shifts");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();
            entity.Property(s => s.ShiftCode).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Description).HasMaxLength(255);
            entity.Property(s => s.StartTime).IsRequired();
            entity.Property(s => s.EndTime).IsRequired();
            entity.Property(s => s.GracePeriodMinutes).HasDefaultValue(15);
            entity.Property(s => s.IsNightShift).HasDefaultValue(false);
            entity.Property(s => s.IsRestDay).HasDefaultValue(false);
            entity.Property(s => s.ColorCode).HasMaxLength(20).HasDefaultValue("#1C585C");
            entity.Property(s => s.IsActive).HasDefaultValue(true);
            entity.Property(s => s.CreatedAt).IsRequired();
            entity.Property(s => s.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(s => s.ShiftCode).IsUnique();

            entity.HasData(
                new Shift(1, "MCT-MORN", "Morning Field Shift", "Standard daytime municipal maintenance & inspection", new TimeSpan(7, 0, 0), new TimeSpan(15, 0, 0), 15, false, false, "#1C585C", true, "SYSTEM"),
                new Shift(2, "MCT-EVEN", "Evening Field Shift", "Peak municipal operations & road repairs", new TimeSpan(15, 0, 0), new TimeSpan(23, 0, 0), 15, false, false, "#D97706", true, "SYSTEM"),
                new Shift(3, "MCT-NGHT", "Night Emergency Shift", "Water pipe bursts, drainage & urgent hazards", new TimeSpan(23, 0, 0), new TimeSpan(7, 0, 0), 15, true, false, "#4338CA", true, "SYSTEM"),
                new Shift(4, "MCT-GEN", "General Directorate Hours", "Office & administrative field leads", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 15, false, false, "#059669", true, "SYSTEM"),
                new Shift(5, "MCT-OFF", "Scheduled Rest Day / Off", "Official weekly off / compensatory rest", TimeSpan.Zero, TimeSpan.Zero, 0, false, true, "#64748B", true, "SYSTEM")
            );
        });

        // ── RosterAssignments ───────────────────────────────────────────────
        modelBuilder.Entity<RosterAssignment>(entity =>
        {
            entity.ToTable("RosterAssignments");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedOnAdd();
            entity.Property(r => r.EmployeeId).IsRequired();
            entity.Property(r => r.ShiftId).IsRequired();
            entity.Property(r => r.RosterDate).IsRequired();
            entity.Property(r => r.Zone).HasMaxLength(100);
            entity.Property(r => r.Notes).HasMaxLength(500);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(r => r.CreatedAt).IsRequired();
            entity.Property(r => r.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasOne(r => r.Employee)
                .WithMany()
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Shift)
                .WithMany()
                .HasForeignKey(r => r.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.EmployeeId, r.RosterDate })
                .HasDatabaseName("IX_RosterAssignments_Employee_RosterDate");
        });

        modelBuilder.Entity<FFM.Identity.Access.Domain.Entities.UserDevice>(entity =>
        {
            entity.ToTable("UserDevices");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DeviceUuid).IsRequired();
        });
    }
}
