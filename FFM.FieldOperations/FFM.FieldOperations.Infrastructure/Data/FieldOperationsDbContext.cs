using FFM.FieldOperations.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.FieldOperations.Infrastructure.Data
{
    public class FieldOperationsDbContext : DbContext
    {
        public FieldOperationsDbContext(DbContextOptions<FieldOperationsDbContext> options)
            : base(options)
        {
        }

        public DbSet<ClientSite> ClientSites => Set<ClientSite>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();
        public DbSet<Visit> Visits => Set<Visit>();
        public DbSet<VisitEvidence> VisitEvidences => Set<VisitEvidence>();
        public DbSet<EvidenceType> EvidenceTypes => Set<EvidenceType>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ClientSite>(entity =>
            {
                entity.ToTable("ClientSites");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Id).ValueGeneratedOnAdd();
                entity.Property(s => s.SiteCode).IsRequired().HasMaxLength(50);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(150);
                entity.Property(s => s.ContactPerson).HasMaxLength(100);
                entity.Property(s => s.ContactPhone).HasMaxLength(30);
                entity.Property(s => s.ContactEmail).HasMaxLength(150);
                entity.Property(s => s.Latitude).HasPrecision(10, 7).IsRequired();
                entity.Property(s => s.Longitude).HasPrecision(10, 7).IsRequired();
                entity.Property(s => s.RadiusMeters).IsRequired().HasDefaultValue(150);
                entity.Property(s => s.Address).IsRequired().HasMaxLength(300);
                entity.Property(s => s.City).HasMaxLength(100);
                entity.Property(s => s.State).HasMaxLength(100);
                entity.Property(s => s.PostalCode).HasMaxLength(20);
                entity.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);
                entity.Property(s => s.CreatedByUserId).IsRequired();
                entity.Property(s => s.CreatedAt).IsRequired();
                entity.Property(s => s.UpdatedAt).IsRequired();

                entity.HasIndex(s => s.SiteCode)
                    .IsUnique()
                    .HasDatabaseName("IX_ClientSites_SiteCode_Unique");

                entity.HasIndex(s => new { s.Latitude, s.Longitude })
                    .HasDatabaseName("IX_ClientSites_Coordinates");

                entity.HasIndex(s => s.IsActive)
                    .HasDatabaseName("IX_ClientSites_IsActive");
            });

            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("Tasks");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Id).ValueGeneratedOnAdd();
                entity.Property(t => t.TaskNumber).IsRequired().HasMaxLength(50);
                entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
                entity.Property(t => t.Description).HasColumnType("text");
                entity.Property(t => t.Priority).IsRequired().HasMaxLength(20).HasDefaultValue("MEDIUM");
                entity.Property(t => t.Status).IsRequired().HasMaxLength(30).HasDefaultValue("CREATED");
                entity.Property(t => t.TaskType).IsRequired().HasMaxLength(50).HasDefaultValue("GENERAL");
                entity.Property(t => t.CustomDataJson).HasColumnType("text").IsRequired(false);
                entity.Property(t => t.ClientSiteId).IsRequired();
                entity.Property(t => t.ScheduledStart).IsRequired();
                entity.Property(t => t.ScheduledEnd).IsRequired();
                entity.Property(t => t.CreatedByUserId).IsRequired();
                entity.Property(t => t.CancellationReason).HasMaxLength(500);
                entity.Property(t => t.CreatedAt).IsRequired();
                entity.Property(t => t.UpdatedAt).IsRequired();

                entity.HasOne(t => t.ClientSite)
                    .WithMany()
                    .HasForeignKey(t => t.ClientSiteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => t.TaskNumber).IsUnique().HasDatabaseName("IX_Tasks_TaskNumber");
                entity.HasIndex(t => t.ClientSiteId).HasDatabaseName("IX_Tasks_ClientSiteId");
                entity.HasIndex(t => new { t.AssignedEmployeeId, t.Status }).HasDatabaseName("IX_Tasks_AssignedEmployeeId_Status");
                entity.HasIndex(t => new { t.ScheduledStart, t.ScheduledEnd }).HasDatabaseName("IX_Tasks_Schedule");
                entity.HasIndex(t => t.Status).HasDatabaseName("IX_Tasks_Status");
            });

            modelBuilder.Entity<Visit>(entity =>
            {
                entity.ToTable("Visits");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Id).ValueGeneratedOnAdd();
                entity.Property(v => v.TaskId).IsRequired();
                entity.Property(v => v.EmployeeId).IsRequired();
                entity.Property(v => v.CheckInTime).IsRequired();
                entity.Property(v => v.CheckInLatitude).HasPrecision(10, 7).IsRequired();
                entity.Property(v => v.CheckInLongitude).HasPrecision(10, 7).IsRequired();
                entity.Property(v => v.CheckInDistanceMeters).HasPrecision(8, 2).IsRequired();
                entity.Property(v => v.IsWithinGeofence).IsRequired().HasDefaultValue(true);
                entity.Property(v => v.CheckOutTime);
                entity.Property(v => v.CheckOutLatitude).HasPrecision(10, 7);
                entity.Property(v => v.CheckOutLongitude).HasPrecision(10, 7);
                entity.Property(v => v.DurationMinutes).HasPrecision(6, 2);
                entity.Property(v => v.CompletionNotes).HasColumnType("text");
                entity.Property(v => v.CustomerName).HasMaxLength(100);
                entity.Property(v => v.CustomerSignatureUrl).HasMaxLength(500);
                entity.Property(v => v.CustomerRating);
                entity.Property(v => v.Status).IsRequired().HasMaxLength(30).HasDefaultValue("CHECKED_IN");
                entity.Property(v => v.CreatedAt).IsRequired();

                entity.HasOne(v => v.Task)
                    .WithMany()
                    .HasForeignKey(v => v.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => v.TaskId).HasDatabaseName("IX_Visits_TaskId");
                entity.HasIndex(v => v.CheckInTime).HasDatabaseName("IX_Visits_CheckInTime");
                entity.HasIndex(v => new { v.EmployeeId, v.Status }).HasDatabaseName("IX_Visits_Employee_Status");
            });

            modelBuilder.Entity<EvidenceType>(entity =>
            {
                entity.ToTable("EvidenceTypes");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Id).ValueGeneratedNever();
                entity.Property(t => t.Code).IsRequired().HasMaxLength(50);
                entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
                entity.Property(t => t.Description).HasMaxLength(250);
                entity.Property(t => t.WidgetType).IsRequired().HasMaxLength(50);
                entity.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);
                entity.Property(t => t.CreatedAt).IsRequired();

                entity.HasIndex(t => t.Code).IsUnique();
            });

            modelBuilder.Entity<VisitEvidence>(entity =>
            {
                entity.ToTable("VisitEvidences");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.VisitId).IsRequired();
                entity.Property(e => e.EvidenceTypeId).IsRequired().HasDefaultValue(1);
                entity.Property(e => e.EvidenceType).IsRequired().HasMaxLength(30);
                entity.Property(e => e.FileName).HasMaxLength(255);
                entity.Property(e => e.FolderPath).HasMaxLength(500);
                entity.Property(e => e.FileUrl).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
                entity.Property(e => e.Remarks).HasMaxLength(500);
                entity.Property(e => e.CapturedLatitude).HasPrecision(10, 7);
                entity.Property(e => e.CapturedLongitude).HasPrecision(10, 7);
                entity.Property(e => e.CapturedAt).IsRequired();
                entity.Property(e => e.ReadingsJson).HasColumnType("text").IsRequired(false);

                entity.HasOne(e => e.Visit)
                    .WithMany(v => v.Evidences)
                    .HasForeignKey(e => e.VisitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.EvidenceTypeMaster)
                    .WithMany()
                    .HasForeignKey(e => e.EvidenceTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.VisitId).HasDatabaseName("IX_VisitEvidences_VisitId");
                entity.HasIndex(e => e.EvidenceTypeId).HasDatabaseName("IX_VisitEvidences_EvidenceTypeId");
                entity.HasIndex(e => e.EvidenceType).HasDatabaseName("IX_VisitEvidences_Type");
                entity.HasIndex(e => e.CapturedAt).HasDatabaseName("IX_VisitEvidences_CapturedAt");
            });
        }
    }
}
