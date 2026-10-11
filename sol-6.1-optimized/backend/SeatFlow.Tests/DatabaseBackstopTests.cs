using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Domain;

namespace SeatFlow.Tests;

public sealed class DatabaseBackstopTests
{
    [Fact]
    public async Task Direct_insert_cannot_exceed_capacity()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync());
        var excess = await fixture.ParticipantAsync("Excess attendee");
        await using var bypass = fixture.NewContext();
        bypass.Registrations.Add(Row(workshop.Id, excess.Id));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => bypass.SaveChangesAsync());

        Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
        Assert.Single(await fixture.RegistrationsAsync());
    }

    [Fact]
    public async Task Direct_promotion_cannot_exceed_capacity()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync());
        var queued = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Queued attendee"));
        await using var bypass = fixture.NewContext();
        var row = await bypass.Registrations.SingleAsync(r => r.Id == queued.Registration.Id);
        row.Status = RegistrationStatus.Confirmed;

        await Assert.ThrowsAsync<DbUpdateException>(() => bypass.SaveChangesAsync());

        var persisted = await fixture.RegistrationsAsync();
        Assert.Single(persisted, r => r.Status == RegistrationStatus.Confirmed);
        Assert.Single(persisted, r => r.Status == RegistrationStatus.Waitlisted);
    }

    [Fact]
    public async Task Direct_insert_cannot_create_overlapping_confirmed_schedule()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = await fixture.WorkshopAsync(startHour: 10, endHour: 12);
        var second = await fixture.WorkshopAsync(startHour: 11, endHour: 13);
        var participant = await fixture.ParticipantAsync();
        await fixture.RegisterAsync(first, participant);
        await using var bypass = fixture.NewContext();
        bypass.Registrations.Add(Row(second.Id, participant.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => bypass.SaveChangesAsync());

        Assert.Single(await fixture.RegistrationsAsync());
    }

    [Fact]
    public async Task Direct_insert_cannot_create_duplicate_active_registration()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync(capacity: 3);
        var participant = await fixture.ParticipantAsync();
        await fixture.RegisterAsync(workshop, participant);
        await using var bypass = fixture.NewContext();
        bypass.Registrations.Add(Row(workshop.Id, participant.Id, RegistrationStatus.Waitlisted));

        await Assert.ThrowsAsync<DbUpdateException>(() => bypass.SaveChangesAsync());

        Assert.Single(await fixture.RegistrationsAsync());
    }

    [Fact]
    public async Task Direct_capacity_reduction_cannot_remove_occupied_seats()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync(capacity: 2);
        await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("First attendee"));
        await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Second attendee"));
        await using var bypass = fixture.NewContext();
        var row = await bypass.Workshops.SingleAsync(w => w.Id == workshop.Id);
        row.Capacity = 1;

        await Assert.ThrowsAsync<DbUpdateException>(() => bypass.SaveChangesAsync());

        await using var fresh = fixture.NewContext();
        Assert.Equal(2, (await fresh.Workshops.SingleAsync()).Capacity);
        Assert.Equal(2, await fresh.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Direct_time_change_cannot_create_a_confirmed_schedule_conflict()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = await fixture.WorkshopAsync(startHour: 10, endHour: 11);
        var second = await fixture.WorkshopAsync(startHour: 11, endHour: 12);
        var participant = await fixture.ParticipantAsync();
        await fixture.RegisterAsync(first, participant);
        await fixture.RegisterAsync(second, participant);
        await using var bypass = fixture.NewContext();
        var row = await bypass.Workshops.SingleAsync(w => w.Id == first.Id);
        row.End = row.End.AddMinutes(1);

        await Assert.ThrowsAsync<DbUpdateException>(() => bypass.SaveChangesAsync());

        await using var fresh = fixture.NewContext();
        Assert.Equal(first.End, (await fresh.Workshops.SingleAsync(w => w.Id == first.Id)).End);
    }

    private static Registration Row(long workshopId, long participantId, RegistrationStatus status = RegistrationStatus.Confirmed) =>
        new() { WorkshopId = workshopId, ParticipantId = participantId, Status = status, Created = new DateTime(2030, 6, 12, 8, 0, 0, DateTimeKind.Utc) };
}
