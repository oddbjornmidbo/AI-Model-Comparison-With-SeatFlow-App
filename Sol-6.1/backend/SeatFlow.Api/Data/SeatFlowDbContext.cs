using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Data;

public sealed class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite does not retain DateTime.Kind. Restore UTC when materializing.
        var utc = new ValueConverter<DateTime, DateTime>(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        modelBuilder.Entity<Workshop>(entity =>
        {
            entity.Property(w => w.Title).HasMaxLength(200).IsRequired();
            entity.Property(w => w.StartTime).HasConversion(utc);
            entity.Property(w => w.EndTime).HasConversion(utc);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Workshop_Capacity", "\"Capacity\" > 0");
                table.HasCheckConstraint("CK_Workshop_Times", "\"EndTime\" > \"StartTime\"");
            });
        });
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(p => p.Email).IsUnique();
        });
        modelBuilder.Entity<Registration>(entity =>
        {
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.CreatedAt).HasConversion(utc);
            entity.HasIndex(r => new { r.WorkshopId, r.ParticipantId })
                .IsUnique().HasFilter("\"Status\" <> 'Cancelled'");
            entity.HasIndex(r => new { r.WorkshopId, r.Status, r.CreatedAt });
            entity.HasOne(r => r.Workshop).WithMany().HasForeignKey(r => r.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Participant).WithMany().HasForeignKey(r => r.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(i => i.Key);
            entity.Property(i => i.Key).HasMaxLength(200);
            entity.HasOne(i => i.Registration).WithMany().HasForeignKey(i => i.RegistrationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
