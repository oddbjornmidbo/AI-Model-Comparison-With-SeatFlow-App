using Microsoft.EntityFrameworkCore;

namespace SeatFlow.Api;

public sealed class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workshop>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_Workshop_Capacity", "Capacity > 0"));
            entity.ToTable(table => table.HasCheckConstraint("CK_Workshop_TimeRange", "StartTime < EndTime"));
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.QueuePosition).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_Registration_QueuePosition", "QueuePosition > 0"));
            entity.HasIndex(x => new { x.WorkshopId, x.QueuePosition }).IsUnique();
            entity.HasIndex(x => new { x.ParticipantId, x.Status });
            entity.HasIndex(x => new { x.WorkshopId, x.ParticipantId }).IsUnique().HasFilter("Status <> 2");
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(200);
            entity.HasIndex(x => x.RegistrationId);
        });
    }
}
