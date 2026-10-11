using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public sealed class RegistrationServiceTests
{
    [Fact]
    public async Task Available_seat_is_confirmed_and_notification_observes_committed_state()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync(capacity: 2);
        var participant = await fixture.ParticipantAsync();
        var observedCommitted = false;
        fixture.Notifications.Observer = async notification =>
        {
            await using var fresh = fixture.NewContext();
            var persisted = await fresh.Registrations.SingleAsync(r => r.Id == notification.RegistrationId);
            Assert.Equal(RegistrationStatus.Confirmed, persisted.Status);
            observedCommitted = true;
        };

        var result = await fixture.RegisterAsync(workshop, participant);

        Assert.Equal(RegistrationStatus.Confirmed, result.Registration.Status);
        Assert.False(result.IsReplay);
        Assert.True(observedCommitted);
        Assert.Equal(NotificationKind.Confirmed, Assert.Single(fixture.Notifications.Sent).Kind);
        Assert.Equal(RegistrationStatus.Confirmed, Assert.Single(await fixture.RegistrationsAsync()).Status);
    }

    [Fact]
    public async Task Full_workshop_creates_a_waitlist_entry_without_consuming_a_seat()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Taylor Chen"));
        var result = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Morgan Patel"));

        Assert.Equal(RegistrationStatus.Waitlisted, result.Registration.Status);
        var rows = await fixture.RegistrationsAsync();
        Assert.Single(rows, r => r.Status == RegistrationStatus.Confirmed);
        Assert.Single(rows, r => r.Status == RegistrationStatus.Waitlisted);
        Assert.Single(fixture.Notifications.Sent);
    }

    [Fact]
    public async Task Cancellation_promotes_only_the_first_waitlisted_participant()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var owner = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Jamie Wilson"));
        var first = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Riley Santos"));
        var second = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Sam Okafor"));

        var cancellation = await fixture.CancelAsync(owner.Registration.Id);

        Assert.Equal(first.Registration.Id, cancellation.PromotedRegistration?.Id);
        var rows = await fixture.RegistrationsAsync();
        Assert.Equal(RegistrationStatus.Cancelled, rows.Single(r => r.Id == owner.Registration.Id).Status);
        Assert.Equal(RegistrationStatus.Confirmed, rows.Single(r => r.Id == first.Registration.Id).Status);
        Assert.Equal(RegistrationStatus.Waitlisted, rows.Single(r => r.Id == second.Registration.Id).Status);
        Assert.Single(fixture.Notifications.Sent, n => n.Kind == NotificationKind.Promoted);
    }

    [Fact]
    public async Task Promotion_skips_conflicting_participant_and_preserves_their_fifo_position()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var target = await fixture.WorkshopAsync(startHour: 10, endHour: 12);
        var overlap = await fixture.WorkshopAsync(startHour: 11, endHour: 13);
        var owner = await fixture.RegisterAsync(target, await fixture.ParticipantAsync("Jordan Reed"));
        var conflictingParticipant = await fixture.ParticipantAsync("Casey Kim");
        var conflictingBooking = await fixture.RegisterAsync(overlap, conflictingParticipant);
        var skipped = await fixture.RegisterAsync(target, conflictingParticipant);
        var eligible = await fixture.RegisterAsync(target, await fixture.ParticipantAsync("Avery Singh"));

        var firstCancellation = await fixture.CancelAsync(owner.Registration.Id);
        Assert.Equal(eligible.Registration.Id, firstCancellation.PromotedRegistration?.Id);
        Assert.Equal(RegistrationStatus.Waitlisted,
            (await fixture.RegistrationsAsync()).Single(r => r.Id == skipped.Registration.Id).Status);

        var newcomer = await fixture.RegisterAsync(target, await fixture.ParticipantAsync("Robin Berg"));
        await fixture.CancelAsync(conflictingBooking.Registration.Id);
        var nextCancellation = await fixture.CancelAsync(eligible.Registration.Id);

        Assert.Equal(skipped.Registration.Id, nextCancellation.PromotedRegistration?.Id);
        var rows = await fixture.RegistrationsAsync();
        Assert.Equal(RegistrationStatus.Confirmed, rows.Single(r => r.Id == skipped.Registration.Id).Status);
        Assert.Equal(RegistrationStatus.Waitlisted, rows.Single(r => r.Id == newcomer.Registration.Id).Status);
    }

    [Fact]
    public async Task An_available_overlapping_workshop_is_rejected_but_a_full_one_allows_waitlisting()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = await fixture.WorkshopAsync(startHour: 10, endHour: 12);
        var overlap = await fixture.WorkshopAsync(startHour: 11, endHour: 13);
        var participant = await fixture.ParticipantAsync();
        await fixture.RegisterAsync(first, participant);

        var conflict = await Assert.ThrowsAsync<DomainException>(() => fixture.RegisterAsync(overlap, participant));
        Assert.Equal(409, conflict.StatusCode);
        Assert.Single(await fixture.RegistrationsAsync());

        await fixture.RegisterAsync(overlap, await fixture.ParticipantAsync("Devon Ali"));
        var waitlisted = await fixture.RegisterAsync(overlap, participant);
        Assert.Equal(RegistrationStatus.Waitlisted, waitlisted.Registration.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_active_registration_is_rejected_for_confirmed_and_waitlisted_states(bool full)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        if (full) await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Existing attendee"));
        var participant = await fixture.ParticipantAsync();
        var original = await fixture.RegisterAsync(workshop, participant);

        var conflict = await Assert.ThrowsAsync<DomainException>(() => fixture.RegisterAsync(workshop, participant));

        Assert.Equal(409, conflict.StatusCode);
        Assert.Single((await fixture.RegistrationsAsync()), r => r.ParticipantId == participant.Id);
        Assert.Equal(full ? RegistrationStatus.Waitlisted : RegistrationStatus.Confirmed, original.Registration.Status);
    }

    [Fact]
    public async Task Cancelled_participant_can_register_again_with_a_new_registration_and_key()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var participant = await fixture.ParticipantAsync();
        var original = await fixture.RegisterAsync(workshop, participant, "original-attempt");
        await fixture.CancelAsync(original.Registration.Id);

        var replacement = await fixture.RegisterAsync(workshop, participant, "new-attempt");

        Assert.NotEqual(original.Registration.Id, replacement.Registration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, replacement.Registration.Status);
        var rows = await fixture.RegistrationsAsync();
        Assert.Equal(2, rows.Length);
        Assert.Single(rows, r => r.Status == RegistrationStatus.Cancelled);
        Assert.Single(rows, r => r.Status == RegistrationStatus.Confirmed);
    }

    [Fact]
    public async Task Idempotency_replay_survives_new_contexts_and_does_not_repeat_notifications()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var participant = await fixture.ParticipantAsync();
        var original = await fixture.RegisterAsync(workshop, participant, "same-logical-attempt");

        var replay = await fixture.RegisterAsync(workshop, participant, "same-logical-attempt");

        Assert.True(replay.IsReplay);
        Assert.Equal(original.Registration, replay.Registration);
        Assert.Single(await fixture.RegistrationsAsync());
        Assert.Single(fixture.Notifications.Sent);
        await using var fresh = fixture.NewContext();
        Assert.Equal(1, await fresh.IdempotencyRequests.CountAsync());
    }

    [Fact]
    public async Task Replaying_an_old_key_after_cancellation_cannot_create_a_new_booking()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var participant = await fixture.ParticipantAsync();
        var original = await fixture.RegisterAsync(workshop, participant, "old-attempt");
        await fixture.CancelAsync(original.Registration.Id);

        var replay = await fixture.RegisterAsync(workshop, participant, "old-attempt");

        Assert.True(replay.IsReplay);
        Assert.Equal(original.Registration.Id, replay.Registration.Id);
        Assert.Equal(RegistrationStatus.Cancelled, Assert.Single(await fixture.RegistrationsAsync()).Status);
        Assert.Equal(2, fixture.Notifications.Sent.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Same_idempotency_key_with_a_different_request_returns_conflict(bool differentParticipant)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync(capacity: 2);
        var participant = await fixture.ParticipantAsync();
        await fixture.RegisterAsync(workshop, participant, "bound-key");
        var otherWorkshop = differentParticipant ? workshop : await fixture.WorkshopAsync(startHour: 12, endHour: 13);
        var otherParticipant = differentParticipant ? await fixture.ParticipantAsync("Different participant") : participant;

        var conflict = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.RegisterAsync(otherWorkshop, otherParticipant, "bound-key"));

        Assert.Equal(409, conflict.StatusCode);
        Assert.Single(await fixture.RegistrationsAsync());
        await using var fresh = fixture.NewContext();
        Assert.Equal(1, await fresh.IdempotencyRequests.CountAsync());
    }

    [Fact]
    public async Task Cancelling_twice_preserves_capacity_and_promotes_only_once()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var owner = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync());
        var first = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("First in line"));
        var second = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Second in line"));
        await fixture.CancelAsync(owner.Registration.Id);
        var before = (await fixture.RegistrationsAsync()).Select(r => (r.Id, r.Status)).ToArray();
        var notificationsBefore = fixture.Notifications.Sent.Count;

        var repeated = await fixture.CancelAsync(owner.Registration.Id);

        Assert.Null(repeated.PromotedRegistration);
        Assert.Equal(before, (await fixture.RegistrationsAsync()).Select(r => (r.Id, r.Status)).ToArray());
        Assert.Equal(notificationsBefore, fixture.Notifications.Sent.Count);
        Assert.Equal(RegistrationStatus.Confirmed, (await fixture.RegistrationsAsync()).Single(r => r.Id == first.Registration.Id).Status);
        Assert.Equal(RegistrationStatus.Waitlisted, (await fixture.RegistrationsAsync()).Single(r => r.Id == second.Registration.Id).Status);
    }

    [Fact]
    public async Task Concurrent_final_seat_attempts_use_independent_connections_and_persist_exactly_one_confirmation()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync(capacity: 2);
        await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Already attending"));
        var participants = new List<Participant>();
        for (var index = 0; index < 8; index++) participants.Add(await fixture.ParticipantAsync($"Contender {index}"));
        var callers = participants.Select<Participant, Func<SeatFlow.Api.Data.SeatFlowDbContext, Task<RegistrationResult>>>(participant =>
            db => fixture.Service(db).RegisterAsync(workshop.Id, participant.Id, $"seat-race-{participant.Id}")).ToArray();

        await fixture.RaceAsync(callers);

        var persisted = await fixture.RegistrationsAsync();
        Assert.Equal(9, persisted.Length);
        Assert.Equal(2, persisted.Count(r => r.Status == RegistrationStatus.Confirmed));
        Assert.Equal(7, persisted.Count(r => r.Status == RegistrationStatus.Waitlisted));
        Assert.Equal(2, fixture.Notifications.Sent.Count(n => n.Kind == NotificationKind.Confirmed));
        await using var fresh = fixture.NewContext();
        Assert.Equal(9, await fresh.IdempotencyRequests.CountAsync());
    }

    [Fact]
    public async Task Concurrent_same_key_attempts_persist_one_registration_one_key_and_one_notification()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var participant = await fixture.ParticipantAsync();
        var callers = Enumerable.Range(0, 8).Select<int, Func<SeatFlow.Api.Data.SeatFlowDbContext, Task<RegistrationResult>>>(_ =>
            db => fixture.Service(db).RegisterAsync(workshop.Id, participant.Id, "contended-key")).ToArray();

        var results = await fixture.RaceAsync(callers);

        Assert.Single(results.Select(r => r.Registration.Id).Distinct());
        Assert.Single(results, r => !r.IsReplay);
        var persisted = Assert.Single(await fixture.RegistrationsAsync());
        Assert.Equal(RegistrationStatus.Confirmed, persisted.Status);
        Assert.Equal(results[0].Registration.Id, persisted.Id);
        Assert.Single(fixture.Notifications.Sent);
        await using var fresh = fixture.NewContext();
        Assert.Equal(1, await fresh.IdempotencyRequests.CountAsync());
    }

    [Fact]
    public async Task Concurrent_same_key_for_different_participants_cannot_silently_replay_an_unrelated_registration()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync(capacity: 2);
        var first = await fixture.ParticipantAsync("First key contender");
        var second = await fixture.ParticipantAsync("Second key contender");

        var results = await fixture.RaceAsync(
            db => CaptureAsync(() => fixture.Service(db).RegisterAsync(workshop.Id, first.Id, "same-key-different-request")),
            db => CaptureAsync(() => fixture.Service(db).RegisterAsync(workshop.Id, second.Id, "same-key-different-request")));

        Assert.Single(results, result => result is null);
        var conflict = Assert.Single(results.OfType<DomainException>());
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal("idempotency_key_conflict", conflict.Code);
        var persisted = Assert.Single(await fixture.RegistrationsAsync());
        Assert.Contains(persisted.ParticipantId, new[] { first.Id, second.Id });
        Assert.Equal(RegistrationStatus.Confirmed, persisted.Status);
        Assert.Single(fixture.Notifications.Sent);
        await using var fresh = fixture.NewContext();
        Assert.Equal(1, await fresh.IdempotencyRequests.CountAsync());
    }

    [Fact]
    public async Task Concurrent_cancellations_of_the_same_registration_promote_and_notify_exactly_once()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var owner = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Current attendee"));
        var first = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("First waiting attendee"));
        var second = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Second waiting attendee"));

        var results = await fixture.RaceAsync(
            db => fixture.Service(db).CancelAsync(owner.Registration.Id),
            db => fixture.Service(db).CancelAsync(owner.Registration.Id));

        Assert.All(results, result => Assert.Equal(RegistrationStatus.Cancelled, result.Registration.Status));
        Assert.Equal(first.Registration.Id, Assert.Single(results, result => result.PromotedRegistration is not null).PromotedRegistration!.Id);
        Assert.Single(results, result => result.PromotedRegistration is null);
        var persisted = await fixture.RegistrationsAsync();
        Assert.Equal(RegistrationStatus.Cancelled, persisted.Single(r => r.Id == owner.Registration.Id).Status);
        Assert.Equal(RegistrationStatus.Confirmed, persisted.Single(r => r.Id == first.Registration.Id).Status);
        Assert.Equal(RegistrationStatus.Waitlisted, persisted.Single(r => r.Id == second.Registration.Id).Status);
        Assert.Single(fixture.Notifications.Sent, notification => notification.Kind == NotificationKind.Cancelled);
        Assert.Single(fixture.Notifications.Sent, notification => notification.Kind == NotificationKind.Promoted);
    }

    [Fact]
    public async Task Exactly_touching_time_boundaries_allow_both_confirmations()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var morning = await fixture.WorkshopAsync(startHour: 10, endHour: 11);
        var next = await fixture.WorkshopAsync(startHour: 11, endHour: 12);
        var participant = await fixture.ParticipantAsync();

        await fixture.RegisterAsync(morning, participant);
        await fixture.RegisterAsync(next, participant);

        Assert.Equal(2, (await fixture.RegistrationsAsync()).Count(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Notification_failure_cannot_roll_back_a_successful_registration_or_replay_it()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        fixture.Notifications.Throw = true;
        var workshop = await fixture.WorkshopAsync();
        var participant = await fixture.ParticipantAsync();

        var registered = await fixture.RegisterAsync(workshop, participant, "notification-outage");
        var replay = await fixture.RegisterAsync(workshop, participant, "notification-outage");

        Assert.Equal(registered.Registration.Id, replay.Registration.Id);
        Assert.True(replay.IsReplay);
        Assert.Equal(RegistrationStatus.Confirmed, Assert.Single(await fixture.RegistrationsAsync()).Status);
        Assert.Single(fixture.Notifications.Sent);
    }

    [Fact]
    public async Task Notification_failure_cannot_roll_back_atomic_cancellation_and_promotion()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var owner = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync());
        var queued = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Queued attendee"));
        var committedSnapshots = new List<RegistrationStatus[]>();
        fixture.Notifications.Observer = async _ =>
            committedSnapshots.Add((await fixture.RegistrationsAsync()).Select(r => r.Status).ToArray());
        fixture.Notifications.Throw = true;

        var cancellation = await fixture.CancelAsync(owner.Registration.Id);

        Assert.Equal(queued.Registration.Id, cancellation.PromotedRegistration?.Id);
        var expected = new[] { RegistrationStatus.Cancelled, RegistrationStatus.Confirmed };
        Assert.Equal(expected, (await fixture.RegistrationsAsync()).Select(r => r.Status).ToArray());
        Assert.Equal(2, committedSnapshots.Count);
        Assert.All(committedSnapshots, snapshot => Assert.Equal(expected, snapshot));
        Assert.Single(fixture.Notifications.Sent, n => n.Kind == NotificationKind.Cancelled);
        Assert.Single(fixture.Notifications.Sent, n => n.Kind == NotificationKind.Promoted);
    }

    [Fact]
    public async Task Concurrent_overlapping_workshop_requests_preserve_the_persisted_schedule_invariant()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = await fixture.WorkshopAsync(startHour: 10, endHour: 12);
        var second = await fixture.WorkshopAsync(startHour: 11, endHour: 13);
        var participant = await fixture.ParticipantAsync();
        var results = await fixture.RaceAsync(
            db => CaptureAsync(() => fixture.Service(db).RegisterAsync(first.Id, participant.Id, "schedule-first")),
            db => CaptureAsync(() => fixture.Service(db).RegisterAsync(second.Id, participant.Id, "schedule-second")));

        Assert.Single(results, r => r is null);
        Assert.Equal(409, Assert.Single(results.OfType<DomainException>()).StatusCode);
        Assert.Equal(RegistrationStatus.Confirmed, Assert.Single(await fixture.RegistrationsAsync()).Status);
        await using var fresh = fixture.NewContext();
        Assert.Equal(1, await fresh.IdempotencyRequests.CountAsync());
    }

    [Fact]
    public async Task Concurrent_duplicate_requests_with_different_keys_preserve_one_active_registration()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync(capacity: 4);
        var participant = await fixture.ParticipantAsync();

        var results = await fixture.RaceAsync(
            db => CaptureAsync(() => fixture.Service(db).RegisterAsync(workshop.Id, participant.Id, "duplicate-first")),
            db => CaptureAsync(() => fixture.Service(db).RegisterAsync(workshop.Id, participant.Id, "duplicate-second")));

        Assert.Single(results, r => r is null);
        Assert.Equal(409, Assert.Single(results.OfType<DomainException>()).StatusCode);
        Assert.Equal(RegistrationStatus.Confirmed, Assert.Single(await fixture.RegistrationsAsync()).Status);
        Assert.Single(fixture.Notifications.Sent);
    }

    [Fact]
    public async Task Equal_creation_timestamps_are_ordered_deterministically_by_registration_id()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var owner = await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync("Original attendee"));
        var waiting = new List<RegistrationResult>();
        for (var index = 0; index < 4; index++)
            waiting.Add(await fixture.RegisterAsync(workshop, await fixture.ParticipantAsync($"FIFO attendee {index}")));
        Assert.Single((await fixture.RegistrationsAsync()).Select(r => r.Created).Distinct());

        var currentId = owner.Registration.Id;
        foreach (var expected in waiting.OrderBy(r => r.Registration.Id))
        {
            var cancellation = await fixture.CancelAsync(currentId);
            Assert.Equal(expected.Registration.Id, cancellation.PromotedRegistration?.Id);
            currentId = expected.Registration.Id;
        }

        var persisted = await fixture.RegistrationsAsync();
        Assert.Equal(waiting.Last().Registration.Id, Assert.Single(persisted, r => r.Status == RegistrationStatus.Confirmed).Id);
        Assert.Equal(4, persisted.Count(r => r.Status == RegistrationStatus.Cancelled));
    }

    private static async Task<Exception?> CaptureAsync(Func<Task<RegistrationResult>> operation)
    {
        try { await operation(); return null; }
        catch (DomainException exception) { return exception; }
    }
}
