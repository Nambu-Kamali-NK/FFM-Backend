using FFM.GPS.Tracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.GPS.Tracking.Infrastructure
{
    public class GpsDbContext : DbContext
    {
        public GpsDbContext(DbContextOptions<GpsDbContext> options) : base(options) { }

        public DbSet<Geofence> Geofences => Set<Geofence>();
        public DbSet<EmployeeGeofence> EmployeeGeofences => Set<EmployeeGeofence>();
        public DbSet<LocationBreadcrumb> LocationBreadcrumbs => Set<LocationBreadcrumb>();
        public DbSet<GeofenceBreachEvent> GeofenceBreachEvents => Set<GeofenceBreachEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Geofence>(entity =>
            {
                entity.ToTable("Geofences");
                entity.HasKey(g => g.Id);
                entity.Property(g => g.Id).ValueGeneratedOnAdd();
                entity.Property(g => g.LocationRef).IsRequired().HasMaxLength(255);
                entity.HasIndex(g => g.LocationRef).IsUnique();
                entity.Property(g => g.Name).IsRequired().HasMaxLength(255);
                entity.Property(g => g.Type).IsRequired().HasMaxLength(50);
                entity.Property(g => g.BufferMeters).HasDefaultValue(15.0);
                entity.Property(g => g.PolygonCoordinatesJson).HasColumnType("jsonb");
            });

            modelBuilder.Entity<EmployeeGeofence>(entity =>
            {
                entity.ToTable("EmployeeGeofences");
                entity.HasKey(eg => eg.Id);
                entity.Property(eg => eg.Id).ValueGeneratedOnAdd();
                entity.Property(eg => eg.EmployeeId).IsRequired();
                entity.Property(eg => eg.GeofenceId).IsRequired();

                entity.HasOne(eg => eg.Geofence)
                      .WithMany()
                      .HasForeignKey(eg => eg.GeofenceId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(eg => new { eg.EmployeeId, eg.GeofenceId }).IsUnique();
            });

            modelBuilder.Entity<LocationBreadcrumb>(entity =>
            {
                entity.ToTable("LocationBreadcrumbs");
                entity.HasKey(b => b.Id);
                entity.Property(b => b.Id).ValueGeneratedOnAdd();
                entity.Property(b => b.UserId).IsRequired();
                entity.Property(b => b.Latitude).HasPrecision(10, 7).IsRequired();
                entity.Property(b => b.Longitude).HasPrecision(10, 7).IsRequired();
                entity.Property(b => b.AccuracyMeters).HasPrecision(6, 2).IsRequired();
                entity.Property(b => b.SpeedKmh).HasPrecision(6, 2);
                entity.Property(b => b.HeadingDegrees).HasPrecision(5, 2);
                entity.Property(b => b.AltitudeMeters).HasPrecision(7, 2);
                entity.Property(b => b.ActivityType).HasMaxLength(50).HasDefaultValue("UNKNOWN");
                entity.Property(b => b.RecordedAt).IsRequired();

                // Fast index for querying a user's breadcrumbs by date/time
                entity.HasIndex(b => new { b.UserId, b.RecordedAt });
            });

            modelBuilder.Entity<GeofenceBreachEvent>(entity =>
            {
                entity.ToTable("GeofenceBreachEvents");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.GeofenceId).IsRequired();
                entity.Property(e => e.GeofenceName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.LocationRef).IsRequired().HasMaxLength(255);
                entity.Property(e => e.BreachType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Latitude).HasPrecision(10, 7).IsRequired();
                entity.Property(e => e.Longitude).HasPrecision(10, 7).IsRequired();
                entity.Property(e => e.AccuracyMeters).HasPrecision(6, 2);
                entity.Property(e => e.SpeedKmh).HasPrecision(6, 2);
                entity.Property(e => e.TriggeredAt).IsRequired();
                entity.Property(e => e.IsAcknowledged).HasDefaultValue(false);
                entity.Property(e => e.AcknowledgedBy).HasMaxLength(150);
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasIndex(e => new { e.UserId, e.TriggeredAt });
                entity.HasIndex(e => new { e.GeofenceId, e.TriggeredAt });
                entity.HasIndex(e => e.IsAcknowledged);
            });

        }
    }
}
