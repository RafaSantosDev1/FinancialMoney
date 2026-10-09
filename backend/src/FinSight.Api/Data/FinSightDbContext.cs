using FinSight.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Data;

public class FinSightDbContext(DbContextOptions<FinSightDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(a => a.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(a => a.Name);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(80).IsRequired();
            entity.Property(c => c.Color).HasMaxLength(16).IsRequired();
            entity.HasIndex(c => c.Name);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(t => t.Description).HasMaxLength(300).IsRequired();
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.Notes).HasMaxLength(1000);
            entity.Property(t => t.ExternalReference).HasMaxLength(120);

            entity.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.ImportBatch)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.ImportBatchId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(t => t.Date);
            entity.HasIndex(t => new { t.Date, t.Type });
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => t.ImportBatchId);
            entity.HasIndex(t => new { t.Date, t.Description });
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.Property(b => b.LimitAmount).HasPrecision(18, 2);

            entity.HasOne(b => b.Category)
                .WithMany(c => c.Budgets)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // A category can have at most one limit per month.
            entity.HasIndex(b => new { b.CategoryId, b.Year, b.Month })
                .IsUnique()
                .HasFilter("\"CategoryId\" IS NOT NULL");

            // A global budget can exist at most once per month.
            entity.HasIndex(b => new { b.Year, b.Month })
                .IsUnique()
                .HasFilter("\"CategoryId\" IS NULL");

            entity.ToTable(t => t.HasCheckConstraint("CK_Budget_Month", "\"Month\" BETWEEN 1 AND 12"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Budget_Limit", "\"LimitAmount\" >= 0"));
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.Property(b => b.FileName).HasMaxLength(260).IsRequired();
        });
    }
}
