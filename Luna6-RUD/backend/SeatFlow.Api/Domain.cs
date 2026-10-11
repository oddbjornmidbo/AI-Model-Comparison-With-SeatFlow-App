using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SeatFlow.Api;

public enum RegistrationStatus { Confirmed, Waitlisted, Cancelled }

public sealed class Workshop
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Capacity { get; set; }
    public List<Registration> Registrations { get; set; } = [];
}

public sealed class Participant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}

public sealed class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Workshop? Workshop { get; set; }
    public Participant? Participant { get; set; }
}

public sealed class IdempotencyRecord
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public int RegistrationId { get; set; }
}

public sealed class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
        modelBuilder.Entity<Workshop>().Property(x => x.StartsAt).HasConversion(dateTimeOffsetConverter);
        modelBuilder.Entity<Workshop>().Property(x => x.EndsAt).HasConversion(dateTimeOffsetConverter);
        modelBuilder.Entity<Registration>().Property(x => x.CreatedAt).HasConversion(dateTimeOffsetConverter);
        modelBuilder.Entity<Workshop>().Property(x => x.Title).IsRequired();
        modelBuilder.Entity<Participant>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Registration>().HasIndex(x => new { x.WorkshopId, x.ParticipantId });
        modelBuilder.Entity<Registration>().HasOne(x => x.Workshop).WithMany(x => x.Registrations).HasForeignKey(x => x.WorkshopId);
        modelBuilder.Entity<Registration>().HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId);
        modelBuilder.Entity<IdempotencyRecord>().HasIndex(x => x.Key).IsUnique();
    }
}
