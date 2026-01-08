using LastShot.Models;
using Microsoft.EntityFrameworkCore;

namespace LastShot.Data;

public class LastShotDbContext : DbContext
{
    public LastShotDbContext(DbContextOptions<LastShotDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<RadioAsset> RadioAssets => Set<RadioAsset>();
    public DbSet<FrequencyChannel> FrequencyChannels => Set<FrequencyChannel>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<ComplianceLogEntry> ComplianceLogs => Set<ComplianceLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.Email)
            .IsUnique();

        modelBuilder.Entity<RadioAsset>()
            .HasIndex(r => r.SerialNumber)
            .IsUnique();

        modelBuilder.Entity<RentalContract>()
            .HasOne(rc => rc.Customer)
            .WithMany(c => c.RentalContracts)
            .HasForeignKey(rc => rc.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasOne(rc => rc.RadioAsset)
            .WithMany(r => r.RentalContracts)
            .HasForeignKey(rc => rc.RadioAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasOne(rc => rc.FrequencyChannel)
            .WithMany(fc => fc.RentalContracts)
            .HasForeignKey(rc => rc.FrequencyChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasIndex(rc => new { rc.FrequencyChannelId, rc.StartDateUtc, rc.EndDateUtc });

        modelBuilder.Entity<ComplianceLogEntry>()
            .HasOne(log => log.RentalContract)
            .WithMany(contract => contract.ComplianceLogs)
            .HasForeignKey(log => log.RentalContractId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
