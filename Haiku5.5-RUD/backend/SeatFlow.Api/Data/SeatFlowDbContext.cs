using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workshop>(e =>
        {
            e.Property(w => w.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(w => w.StartsAt);
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Email).HasMaxLength(320).IsRequired();
            e.HasIndex(p => p.Email).IsUnique();
        });

        modelBuilder.Entity<Registration>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(r => r.Workshop).WithMany(w => w.Registrations).HasForeignKey(r => r.WorkshopId);
            e.HasOne(r => r.Participant).WithMany().HasForeignKey(r => r.ParticipantId);

            // Database-level guard: a participant has at most one non-cancelled registration per workshop.
            e.HasIndex(r => new { r.WorkshopId, r.ParticipantId })
                .IsUnique()
                .HasFilter("\"Status\" <> 'Cancelled'");

            // Idempotency keys are globally unique; NULLs are allowed (no key supplied).
            e.HasIndex(r => r.IdempotencyKey).IsUnique();
        });
    }
}
