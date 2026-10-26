
using BankGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BankGuard.Api.Data;

public class BankGuardDbContext : DbContext
{
    public BankGuardDbContext(
        DbContextOptions<BankGuardDbContext> options)
        : base(options)
    {
    }

    // Database tables
    public DbSet<BankAccount> Accounts => Set<BankAccount>();

    public DbSet<BankTransaction> Transfers => Set<BankTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================================
        // ACCOUNT CONFIGURATION
        // =========================================

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("Accounts", table =>
            {
                table.HasCheckConstraint(
                    "CK_Accounts_NonNegativeBalance",
                    "[Balance] >= 0");
            });

            entity.HasKey(a => a.Id);

            entity.Property(a => a.OwnerName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.Balance)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(a => a.CreatedAtUtc)
                .IsRequired();
        });

        // =========================================
        // TRANSFER CONFIGURATION
        // =========================================

        modelBuilder.Entity<BankTransaction>(entity =>
        {
            entity.ToTable("Transfers", table =>
            {
                table.HasCheckConstraint(
                    "CK_Transfers_PositiveAmount",
                    "[Amount] > 0");

                table.HasCheckConstraint(
                    "CK_Transfers_DifferentAccounts",
                    "[FromAccountId] <> [ToAccountId]");
            });

            entity.HasKey(t => t.Id);

            entity.Property(t => t.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(t => t.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(t => t.IdempotencyKey)
                .IsUnique();

            entity.Property(t => t.CompletedAtUtc)
                .IsRequired();

            // Source account relationship
            entity.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(t => t.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Destination account relationship
            entity.HasOne<BankAccount>()
                .WithMany()
                .HasForeignKey(t => t.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
