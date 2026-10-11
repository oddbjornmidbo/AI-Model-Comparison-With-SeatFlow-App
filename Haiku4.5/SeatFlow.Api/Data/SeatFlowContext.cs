using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Data;

public class SeatFlowContext : DbContext
{
    public SeatFlowContext(DbContextOptions<SeatFlowContext> options) : base(options) { }

    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Registration>()
            .HasOne(r => r.Workshop)
            .WithMany(w => w.Registrations)
            .HasForeignKey(r => r.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Registration>()
            .HasOne(r => r.Participant)
            .WithMany(p => p.Registrations)
            .HasForeignKey(r => r.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
