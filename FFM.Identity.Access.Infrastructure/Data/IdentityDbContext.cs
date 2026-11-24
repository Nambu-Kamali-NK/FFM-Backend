// File: IdentityDbContext.cs
using Microsoft.EntityFrameworkCore;
using FFM.Identity.Access.Domain.Entities;

namespace FFM.Identity.Access.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure primary keys and relationships
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedOnAdd();
            b.HasIndex(e => e.UsernameOrEmail).IsUnique();
            b.Property(e => e.Role).HasConversion<string>();
        });
        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.TokenHash).IsUnique();
            b.HasOne<User>()
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PasswordResetToken>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.TokenHash).IsUnique();
            b.HasIndex(e => e.UserId);
            b.HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserDevice>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.DeviceUuid).IsUnique();
            b.HasOne<User>()
                .WithMany(u => u.Devices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<MfaChallenge>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AuthAuditLog>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.UserId);
        });
    }
}
