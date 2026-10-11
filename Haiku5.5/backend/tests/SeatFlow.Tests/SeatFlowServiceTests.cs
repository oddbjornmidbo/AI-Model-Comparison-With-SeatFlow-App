using SeatFlow.Core;

namespace SeatFlow.Tests;

public sealed class SeatFlowServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private SeatFlowService Service => _db.Service;

    [Fact]
    public async Task Registering_for_a_workshop_with_free_capacity_confirms_the_registration()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 2);
        var alice = await _db.AddParticipantAsync("Alice");

        var result = await Service.RegisterAsync(workshop.Id, alice.Id, null);

        Assert.True(result.Created);
        Assert.Equal(RegistrationStatus.Confirmed, result.Registration.Status);
        Assert.Contains(_db.Notifications.Sent, n =>
            n.Kind == NotificationKind.RegistrationConfirmed && n.RegistrationId == result.Registration.Id);
    }

    [Fact]
    public async Task Registering_for_a_full_workshop_places_the_registration_on_the_waitlist()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 1);
        var alice = await _db.AddParticipantAsync("Alice");
        var bob = await _db.AddParticipantAsync("Bob");
        await Service.RegisterAsync(workshop.Id, alice.Id, null);

        var result = await Service.RegisterAsync(workshop.Id, bob.Id, null);

        Assert.Equal(RegistrationStatus.Waitlisted, result.Registration.Status);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_registration_promotes_the_oldest_waitlisted_participant()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 1);
        var alice = await _db.AddParticipantAsync("Alice");
        var bob = await _db.AddParticipantAsync("Bob");
        var carol = await _db.AddParticipantAsync("Carol");

        var aliceReg = await Service.RegisterAsync(workshop.Id, alice.Id, null);
        var bobReg = await Service.RegisterAsync(workshop.Id, bob.Id, null);
        var carolReg = await Service.RegisterAsync(workshop.Id, carol.Id, null);

        await Service.CancelAsync(aliceReg.Registration.Id);

        Assert.Equal(RegistrationStatus.Confirmed, (await _db.GetRegistrationAsync(bobReg.Registration.Id)).Status);
        Assert.Equal(RegistrationStatus.Waitlisted, (await _db.GetRegistrationAsync(carolReg.Registration.Id)).Status);
        Assert.Contains(_db.Notifications.Sent, n =>
            n.Kind == NotificationKind.WaitlistPromoted && n.RegistrationId == bobReg.Registration.Id);

        // The next cancellation promotes Carol, who is now the oldest waitlisted registration.
        await Service.CancelAsync(bobReg.Registration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, (await _db.GetRegistrationAsync(carolReg.Registration.Id)).Status);
    }

    [Fact]
    public async Task Promotion_skips_a_waitlisted_participant_with_a_schedule_conflict_and_promotes_the_next_eligible()
    {
        // Seat is contested at 10-12. Bob is confirmed for an overlapping 11-13 workshop, so he must be skipped.
        var contested = await _db.AddWorkshopAsync("Contested", 10, 12, capacity: 1);
        var overlapping = await _db.AddWorkshopAsync("Overlapping", 11, 13, capacity: 5);
        var alice = await _db.AddParticipantAsync("Alice");
        var bob = await _db.AddParticipantAsync("Bob");
        var carol = await _db.AddParticipantAsync("Carol");

        var aliceReg = await Service.RegisterAsync(contested.Id, alice.Id, null);
        await Service.RegisterAsync(overlapping.Id, bob.Id, null);
        var bobWaitlisted = await Service.RegisterAsync(contested.Id, bob.Id, null);
        var carolWaitlisted = await Service.RegisterAsync(contested.Id, carol.Id, null);
        Assert.Equal(RegistrationStatus.Waitlisted, bobWaitlisted.Registration.Status);

        await Service.CancelAsync(aliceReg.Registration.Id);

        Assert.Equal(RegistrationStatus.Waitlisted, (await _db.GetRegistrationAsync(bobWaitlisted.Registration.Id)).Status);
        Assert.Equal(RegistrationStatus.Confirmed, (await _db.GetRegistrationAsync(carolWaitlisted.Registration.Id)).Status);
    }

    [Fact]
    public async Task A_participant_cannot_be_confirmed_for_two_overlapping_workshops()
    {
        var morning = await _db.AddWorkshopAsync("Morning", 10, 12, capacity: 5);
        var overlapping = await _db.AddWorkshopAsync("Overlapping", 11, 13, capacity: 5);
        var alice = await _db.AddParticipantAsync("Alice");
        await Service.RegisterAsync(morning.Id, alice.Id, null);

        await Assert.ThrowsAsync<ConflictException>(() => Service.RegisterAsync(overlapping.Id, alice.Id, null));

        Assert.Equal(1, await _db.CountRegistrationsAsync());
    }

    [Fact]
    public async Task Back_to_back_workshops_do_not_count_as_overlapping()
    {
        var first = await _db.AddWorkshopAsync("First", 9, 11, capacity: 5);
        var second = await _db.AddWorkshopAsync("Second", 11, 12, capacity: 5);
        var alice = await _db.AddParticipantAsync("Alice");
        await Service.RegisterAsync(first.Id, alice.Id, null);

        var result = await Service.RegisterAsync(second.Id, alice.Id, null);

        Assert.Equal(RegistrationStatus.Confirmed, result.Registration.Status);
    }

    [Fact]
    public async Task A_participant_cannot_register_twice_for_the_same_workshop()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 5);
        var alice = await _db.AddParticipantAsync("Alice");
        await Service.RegisterAsync(workshop.Id, alice.Id, null);

        await Assert.ThrowsAsync<ConflictException>(() => Service.RegisterAsync(workshop.Id, alice.Id, null));
    }

    [Fact]
    public async Task A_participant_may_register_again_after_cancelling_their_registration()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 5);
        var alice = await _db.AddParticipantAsync("Alice");
        var first = await Service.RegisterAsync(workshop.Id, alice.Id, null);
        await Service.CancelAsync(first.Registration.Id);

        var second = await Service.RegisterAsync(workshop.Id, alice.Id, null);

        Assert.True(second.Created);
        Assert.NotEqual(first.Registration.Id, second.Registration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, second.Registration.Status);
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_idempotency_key_returns_the_original_registration()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 5);
        var alice = await _db.AddParticipantAsync("Alice");

        var first = await Service.RegisterAsync(workshop.Id, alice.Id, "key-123");
        var replay = await Service.RegisterAsync(workshop.Id, alice.Id, "key-123");

        Assert.True(first.Created);
        Assert.False(replay.Created);
        Assert.Equal(first.Registration.Id, replay.Registration.Id);
        Assert.Equal(1, await _db.CountRegistrationsAsync());
        Assert.Single(_db.Notifications.Sent, n => n.Kind == NotificationKind.RegistrationConfirmed);
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_for_a_different_request_is_rejected()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 5);
        var alice = await _db.AddParticipantAsync("Alice");
        var bob = await _db.AddParticipantAsync("Bob");
        await Service.RegisterAsync(workshop.Id, alice.Id, "shared-key");

        await Assert.ThrowsAsync<ConflictException>(() => Service.RegisterAsync(workshop.Id, bob.Id, "shared-key"));

        Assert.Equal(1, await _db.CountRegistrationsAsync());
    }

    [Fact]
    public async Task Cancelling_twice_returns_the_cancelled_registration_and_does_not_notify_again()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 1);
        var alice = await _db.AddParticipantAsync("Alice");
        var bob = await _db.AddParticipantAsync("Bob");
        var aliceReg = await Service.RegisterAsync(workshop.Id, alice.Id, null);
        var bobReg = await Service.RegisterAsync(workshop.Id, bob.Id, null);

        var first = await Service.CancelAsync(aliceReg.Registration.Id);
        var second = await Service.CancelAsync(aliceReg.Registration.Id);

        Assert.Equal(RegistrationStatus.Cancelled, first.Status);
        Assert.Equal(RegistrationStatus.Cancelled, second.Status);
        // Bob was promoted exactly once, by the first cancellation.
        Assert.Equal(RegistrationStatus.Confirmed, (await _db.GetRegistrationAsync(bobReg.Registration.Id)).Status);
        Assert.Single(_db.Notifications.Sent, n =>
            n.Kind == NotificationKind.RegistrationCancelled && n.RegistrationId == aliceReg.Registration.Id);
    }

    [Fact]
    public async Task Cancelling_an_unknown_registration_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service.CancelAsync(9999));
    }

    [Fact]
    public async Task Concurrent_attempts_for_the_final_seat_confirm_exactly_one_participant()
    {
        var workshop = await _db.AddWorkshopAsync("Last seat", 9, 10, capacity: 1);
        var participants = new List<Participant>();
        for (var i = 0; i < 10; i++)
            participants.Add(await _db.AddParticipantAsync($"Person {i}"));

        var results = await Task.WhenAll(participants.Select(p =>
            Service.RegisterAsync(workshop.Id, p.Id, null)));

        var summary = (await Service.GetWorkshopsAsync()).Single(s => s.Workshop.Id == workshop.Id);
        Assert.Equal(1, results.Count(r => r.Registration.Status == RegistrationStatus.Confirmed));
        Assert.Equal(9, results.Count(r => r.Registration.Status == RegistrationStatus.Waitlisted));
        Assert.Equal(1, summary.ConfirmedCount);
        Assert.Equal(0, summary.RemainingCapacity);
    }

    [Fact]
    public async Task A_failing_notification_does_not_roll_back_a_registration_or_cancellation()
    {
        using var db = new TestDatabase(new FailingNotificationService());
        var workshop = await db.AddWorkshopAsync("Intro", 9, 10, capacity: 1);
        var alice = await db.AddParticipantAsync("Alice");
        var bob = await db.AddParticipantAsync("Bob");

        var aliceReg = await db.Service.RegisterAsync(workshop.Id, alice.Id, null);
        var bobReg = await db.Service.RegisterAsync(workshop.Id, bob.Id, null);
        Assert.Equal(RegistrationStatus.Confirmed, (await db.GetRegistrationAsync(aliceReg.Registration.Id)).Status);

        await db.Service.CancelAsync(aliceReg.Registration.Id);

        Assert.Equal(RegistrationStatus.Cancelled, (await db.GetRegistrationAsync(aliceReg.Registration.Id)).Status);
        Assert.Equal(RegistrationStatus.Confirmed, (await db.GetRegistrationAsync(bobReg.Registration.Id)).Status);
    }

    [Fact]
    public async Task Schedule_lists_only_active_registrations_ordered_by_workshop_start()
    {
        var late = await _db.AddWorkshopAsync("Late", 14, 15, capacity: 5);
        var early = await _db.AddWorkshopAsync("Early", 9, 10, capacity: 1);
        var blocker = await _db.AddParticipantAsync("Blocker");
        var alice = await _db.AddParticipantAsync("Alice");

        var lateReg = await Service.RegisterAsync(late.Id, alice.Id, null);
        await Service.RegisterAsync(early.Id, blocker.Id, null);
        var earlyWaitlisted = await Service.RegisterAsync(early.Id, alice.Id, null);
        await Service.CancelAsync(lateReg.Registration.Id);

        var schedule = await Service.GetParticipantScheduleAsync(alice.Id);

        Assert.Equal([earlyWaitlisted.Registration.Id], schedule.Select(r => r.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, schedule.Single().Status);
    }

    [Fact]
    public async Task Workshop_summary_reports_confirmed_waitlisted_and_remaining_seats()
    {
        var workshop = await _db.AddWorkshopAsync("Intro", 9, 10, capacity: 2);
        for (var i = 0; i < 3; i++)
            await Service.RegisterAsync(workshop.Id, (await _db.AddParticipantAsync($"P{i}")).Id, null);

        var summary = (await Service.GetWorkshopsAsync()).Single();

        Assert.Equal(2, summary.ConfirmedCount);
        Assert.Equal(1, summary.WaitlistedCount);
        Assert.Equal(0, summary.RemainingCapacity);
    }

    [Fact]
    public async Task Creating_a_workshop_that_ends_before_it_starts_is_rejected()
    {
        var error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Service.CreateWorkshopAsync("Backwards", TestDatabase.At(12), TestDatabase.At(9), capacity: 1));

        Assert.Contains("endTime", error.Errors.Keys);
    }
}
