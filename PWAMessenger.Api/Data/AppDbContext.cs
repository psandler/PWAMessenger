using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data.Entities;

namespace PWAMessenger.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<InvitedUser> InvitedUsers => Set<InvitedUser>();
    public DbSet<FcmToken> FcmTokens => Set<FcmToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.UserId);
            e.HasIndex(u => u.Auth0Id).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Auth0Id).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<InvitedUser>(e =>
        {
            e.HasKey(i => i.InvitedUserId);
            e.HasIndex(i => i.Email).IsUnique();
            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.Property(i => i.InvitedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(i => i.InvitedByUser)
             .WithMany()
             .HasForeignKey(i => i.InvitedBy)
             .IsRequired(false);
        });

        modelBuilder.Entity<FcmToken>(e =>
        {
            e.HasKey(f => f.TokenId);
            e.HasIndex(f => new { f.UserId, f.Token }).IsUnique();
            e.Property(f => f.Token).HasMaxLength(500).IsRequired();
            e.Property(f => f.RegisteredAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(f => f.LastSeenAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(f => f.User)
             .WithMany()
             .HasForeignKey(f => f.UserId);
        });
    }
}
