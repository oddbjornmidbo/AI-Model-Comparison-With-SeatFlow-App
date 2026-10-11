using Microsoft.EntityFrameworkCore;

namespace SeatFlow.Core;

public class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workshop>(e =>
        {
            e.Property(w => w.Title).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Email).IsRequired().HasMaxLength(320);
        });

        modelBuilder.Entity<Registration>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.IdempotencyKey).HasMaxLength(255);
            e.HasIndex(r => r.IdempotencyKey).IsUnique();
            e.HasIndex(r => new { r.WorkshopId, r.Status, r.CreatedAt });
            e.HasIndex(r => new { r.ParticipantId, r.Status });
            e.HasOne(r => r.Workshop).WithMany().HasForeignKey(r => r.WorkshopId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Participant).WithMany().HasForeignKey(r => r.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
