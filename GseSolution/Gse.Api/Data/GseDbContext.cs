using Microsoft.EntityFrameworkCore;

namespace Gse.Api.Data;

public class GseDbContext(DbContextOptions<GseDbContext> options) : DbContext(options)
{
    public DbSet<EquipmentCategory> Categories => Set<EquipmentCategory>();
    public DbSet<EquipmentAsset> Assets => Set<EquipmentAsset>();
    public DbSet<MaintenanceOrder> Orders => Set<MaintenanceOrder>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<EquipmentCategory>(e =>
        {
            e.ToTable("EquipmentCategories");
            e.Property(x => x.CategoryCode).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.CategoryCode).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.Description).HasMaxLength(250);
        });

        b.Entity<EquipmentAsset>(e =>
        {
            e.ToTable("EquipmentAssets");
            e.Property(x => x.AssetTag).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.AssetTag).IsUnique();
            e.Property(x => x.OperationalStatus).HasMaxLength(25).IsUnicode(false).IsRequired();
            e.Property(x => x.TotalOperatingHours).HasPrecision(8, 2);
            e.HasOne(x => x.Category).WithMany(c => c.Assets)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<MaintenanceOrder>(e =>
        {
            e.ToTable("MaintenanceOrders");
            e.Property(x => x.OrderNumber).HasMaxLength(30).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.Property(x => x.Priority).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.IssueDescription).HasMaxLength(500).IsRequired();
            e.Property(x => x.ReportedBy).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.ResolutionRemarks).HasMaxLength(500);
            e.HasOne(x => x.Asset).WithMany(a => a.Orders)
                .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("AppUsers");
            e.Property(x => x.Username).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsUnicode(false).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsUnicode(false).IsRequired();
        });
    }
}
