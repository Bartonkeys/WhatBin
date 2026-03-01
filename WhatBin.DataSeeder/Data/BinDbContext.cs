using Microsoft.EntityFrameworkCore;
using WhatBin.DataSeeder.Models;

namespace WhatBin.DataSeeder.Data;

public class BinDbContext : DbContext
{
    public BinDbContext(DbContextOptions<BinDbContext> options) : base(options)
    {
    }

    public DbSet<BinSchedule> BinSchedules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BinSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PostcodeNormalized);
            entity.HasIndex(e => new { e.PostcodeNormalized, e.HouseNumber });
        });
    }
}
