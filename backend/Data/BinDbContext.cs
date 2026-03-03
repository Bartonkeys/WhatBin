using Microsoft.EntityFrameworkCore;
using BelfastBinsApi.Models;

namespace BelfastBinsApi.Data;

public class BinDbContext : DbContext
{
    public BinDbContext(DbContextOptions<BinDbContext> options) : base(options)
    {
    }

    public DbSet<BinSchedule> BinSchedules { get; set; }
    public DbSet<StagingBinSchedule> StagingBinSchedules { get; set; }
    public DbSet<SmsSubscription> SmsSubscriptions { get; set; }
    public DbSet<NotificationLog> NotificationLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BinSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PostcodeNormalized);
            entity.HasIndex(e => new { e.PostcodeNormalized, e.HouseNumber });
        });

        modelBuilder.Entity<StagingBinSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JobCode);
        });

        modelBuilder.Entity<SmsSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PhoneNumber, e.Postcode });
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SmsSubscriptionId, e.CollectionDate });
            entity.HasOne(e => e.Subscription)
                  .WithMany()
                  .HasForeignKey(e => e.SmsSubscriptionId);
        });
    }
}
