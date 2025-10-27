using Microsoft.EntityFrameworkCore;
using MaterialsService.Models;

namespace MaterialsService.Data;

public class MaterialsDbContext : DbContext
{
    public MaterialsDbContext(DbContextOptions<MaterialsDbContext> options) : base(options)
    {
    }

    public DbSet<Material> Materials { get; set; } = null!;
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Material entity
        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.MaterialId);
            entity.Property(e => e.MaterialId).ValueGeneratedOnAdd();
            entity.Property(e => e.CourseId).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.MediaType).HasMaxLength(50);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.ExternalLink).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.CourseId);
            entity.HasIndex(e => new { e.CourseId, e.OrderIndex });
        });

        // Configure PaymentTransaction entity
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId);
            entity.Property(e => e.TransactionId).ValueGeneratedOnAdd();
            entity.Property(e => e.OrderId).HasMaxLength(100);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired().HasDefaultValue("VND");
            entity.Property(e => e.Gateway).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.QrCodeData).HasMaxLength(1000);
            entity.Property(e => e.Payload).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
        });
    }
}


