using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public sealed class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<WorkshopCreationRequest> WorkshopCreationRequests => Set<WorkshopCreationRequest>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var utcTicks = new ValueConverter<DateTime, long>(date => date.Ticks,
            ticks => new DateTime(ticks, DateTimeKind.Utc));

        model.Entity<Workshop>(entity =>
        {
            entity.ToTable("Workshops", table =>
            {
                table.HasCheckConstraint("CK_Workshop_Capacity", "\"Capacity\" > 0 AND \"Capacity\" <= 10000");
                table.HasCheckConstraint("CK_Workshop_Interval", "\"Start\" < \"End\"");
                table.HasCheckConstraint("CK_Workshop_Title", "length(trim(\"Title\")) BETWEEN 1 AND 200");
            });
            entity.Property(w => w.Title).HasMaxLength(200).IsRequired();
            entity.Property(w => w.Start).HasConversion(utcTicks);
            entity.Property(w => w.End).HasConversion(utcTicks);
        });

        model.Entity<Participant>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(p => p.Email).IsUnique();
        });

        model.Entity<Registration>(entity =>
        {
            entity.ToTable("Registrations", table => table.HasCheckConstraint("CK_Registration_Status",
                "\"Status\" IN ('Confirmed', 'Waitlisted', 'Cancelled')"));
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Created).HasConversion(utcTicks);
            entity.HasIndex(r => new { r.WorkshopId, r.ParticipantId }).IsUnique()
                .HasFilter("\"Status\" IN ('Confirmed', 'Waitlisted')");
            entity.HasIndex(r => new { r.WorkshopId, r.Status, r.Created, r.Id });
            entity.HasIndex(r => new { r.ParticipantId, r.Status });
            entity.HasOne(r => r.Workshop).WithMany(w => w.Registrations).HasForeignKey(r => r.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Participant).WithMany(p => p.Registrations).HasForeignKey(r => r.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<IdempotencyRequest>(entity =>
        {
            entity.HasKey(r => r.Key);
            entity.Property(r => r.Key).HasMaxLength(200);
            entity.Property(r => r.Fingerprint).HasMaxLength(200).IsRequired();
            entity.Property(r => r.ResponseJson).IsRequired();
            entity.Property(r => r.Created).HasConversion(utcTicks);
            entity.HasOne(r => r.Registration).WithMany().HasForeignKey(r => r.RegistrationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<WorkshopCreationRequest>(entity =>
        {
            entity.HasKey(r => r.Key);
            entity.Property(r => r.Key).HasMaxLength(200);
            entity.Property(r => r.Fingerprint).HasMaxLength(64).IsRequired();
            entity.Property(r => r.ResponseJson).IsRequired();
            entity.Property(r => r.Created).HasConversion(utcTicks);
            entity.HasOne(r => r.Workshop).WithMany().HasForeignKey(r => r.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
