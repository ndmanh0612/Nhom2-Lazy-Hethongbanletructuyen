using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Identity.Domain.Entities;

namespace PhoneStore.Modules.Identity.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<VerificationChallenge> VerificationChallenges => Set<VerificationChallenge>();
    public DbSet<IdentityAudit> AuditEntries => Set<IdentityAudit>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.VerifiedPhone).HasMaxLength(20);
            entity.HasIndex(e => e.VerifiedPhone).IsUnique().HasFilter("\"VerifiedPhone\" IS NOT NULL");
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasMany(e => e.Addresses)
                .WithOne()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("addresses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ReceiverName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ReceiverPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AddressText).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<VerificationChallenge>(entity =>
        {
            entity.ToTable("verification_challenges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Purpose).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Channel).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Destination).HasMaxLength(254).IsRequired();
            entity.Property(e => e.CodeHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ResetTokenHash).HasMaxLength(64);
            entity.HasIndex(e => new { e.UserId, e.Purpose, e.CreatedAt });
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IdentityAudit>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.HasIndex(e => new { e.CreatedAt, e.Id });
            entity.HasIndex(e => e.ActorId);
            entity.HasIndex(e => e.TargetId);
        });

        // N2LHTBLTT-9: Bảng refresh token cho cơ chế session + xoay vòng token.
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IsRevoked).HasDefaultValue(false);
            entity.Property(e => e.RevokedAt);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
        });
    }
}
