using FFM.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.Notification.Infrastructure.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<EmployeeDevice> EmployeeDevices => Set<EmployeeDevice>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EmployeeDevice>(entity =>
        {
            entity.ToTable("EmployeeDevices");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).ValueGeneratedOnAdd();
            entity.Property(d => d.EmployeeId).IsRequired();
            entity.Property(d => d.DeviceId).IsRequired().HasMaxLength(100);
            entity.Property(d => d.Platform).IsRequired().HasMaxLength(20);
            entity.Property(d => d.DeviceModel).IsRequired().HasMaxLength(100);
            entity.Property(d => d.OsVersion).IsRequired().HasMaxLength(50);
            entity.Property(d => d.AppVersion).IsRequired().HasMaxLength(30);
            entity.Property(d => d.FcmToken).IsRequired().HasMaxLength(500);
            entity.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(d => d.RegisteredAt).IsRequired();
            entity.Property(d => d.LastActiveAt).IsRequired();

            entity.HasIndex(d => d.DeviceId)
                .IsUnique()
                .HasDatabaseName("IX_EmployeeDevices_DeviceId_Unique");

            entity.HasIndex(d => new { d.EmployeeId, d.IsActive })
                .HasDatabaseName("IX_EmployeeDevices_EmployeeId_IsActive");
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("NotificationTemplates");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).ValueGeneratedOnAdd();
            entity.Property(t => t.TemplateCode).IsRequired().HasMaxLength(50);
            entity.Property(t => t.Channel).IsRequired().HasMaxLength(20);
            entity.Property(t => t.Title).HasMaxLength(200);
            entity.Property(t => t.BodyTemplate).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(300);
            entity.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(t => t.CreatedByUserId).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
            entity.Property(t => t.UpdatedAt);

            entity.HasIndex(t => new { t.TemplateCode, t.Channel })
                .IsUnique()
                .HasDatabaseName("IX_NotificationTemplates_Code_Channel_Unique");

            entity.HasIndex(t => new { t.TemplateCode, t.Channel, t.IsActive })
                .HasDatabaseName("IX_NotificationTemplates_Lookup");
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("NotificationLogs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).ValueGeneratedOnAdd();
            entity.Property(l => l.RecipientEmployeeId).IsRequired();
            entity.Property(l => l.TemplateCode).IsRequired().HasMaxLength(50);
            entity.Property(l => l.Channel).IsRequired().HasMaxLength(20);
            entity.Property(l => l.Title).HasMaxLength(250);
            entity.Property(l => l.Body).IsRequired();
            entity.Property(l => l.FcmMessageId).HasMaxLength(250);
            entity.Property(l => l.Status).IsRequired().HasMaxLength(30);
            entity.Property(l => l.ErrorMessage);
            entity.Property(l => l.DispatchedAt).IsRequired();
            entity.Property(l => l.IsRead).IsRequired().HasDefaultValue(false);
            entity.Property(l => l.ReadAt);

            entity.HasIndex(l => l.RecipientEmployeeId)
                .HasDatabaseName("IX_NotificationLogs_RecipientEmployeeId");

            entity.HasIndex(l => new { l.RecipientEmployeeId, l.IsRead })
                .HasDatabaseName("IX_NotificationLogs_Recipient_IsRead");

            entity.HasIndex(l => l.DispatchedAt)
                .HasDatabaseName("IX_NotificationLogs_DispatchedAt");

            entity.HasIndex(l => l.Status)
                .HasDatabaseName("IX_NotificationLogs_Status");
        });
    }
}
