using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public class RegistrationRulesTests : IDisposable
{
    private readonly SeatFlowHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Registering_into_an_available_workshop_confirms_the_seat()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 2);
        var participant = await _h.AddParticipantAsync("Ada");

        var result = await _h.Registrations.RegisterAsync(workshop, participant, idempotencyKey: null);

        Assert.Equal(RegistrationStatus.Confirmed, result.Registration.Status);
        Assert.False(result.Replayed);
        var confirmation = Assert.Single(_h.Recorder.Sent);
        Assert.Equal(NotificationKind.RegistrationConfirmed, confirmation.Kind);
    }

    [Fact]
    public async Task Registering_into_a_full_workshop_places_the_participant_on_the_waitlist()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 1);
        await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Ada"), null);

        var result = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Bjørn"), null);

        Assert.Equal(RegistrationStatus.Waitlisted, result.Registration.Status);
        // Only confirmations and promotions are announced, so waitlisting sends nothing.
        Assert.Single(_h.Recorder.Sent);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_seat_promotes_the_oldest_waitlisted_participant()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 1);
        var first = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Ada"), null);
        var second = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Bjørn"), null);
        var third = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Chen"), null);

        var cancel = await _h.Registrations.CancelAsync(first.Registration.Id);

        Assert.True(cancel.Changed);
        Assert.Equal(second.Registration.Id, cancel.Promoted?.Id);
        Assert.Equal(RegistrationStatus.Confirmed, await _h.StatusOfAsync(second.Registration.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, await _h.StatusOfAsync(third.Registration.Id));
        Assert.Contains(_h.Recorder.Sent, n => n.Kind == NotificationKind.WaitlistPromoted && n.RegistrationId == second.Registration.Id);
        Assert.Contains(_h.Recorder.Sent, n => n.Kind == NotificationKind.RegistrationCancelled && n.RegistrationId == first.Registration.Id);
    }

    [Fact]
    public async Task Promotion_skips_a_waitlisted_participant_who_now_has_a_conflicting_confirmed_workshop()
    {
        // Workshop A (10:00-12:00) has one seat. Bjørn waits for it, then gets a seat in
        // Workshop C (11:00-13:00), which overlaps A. Chen waits after Bjørn.
        var a = await _h.AddWorkshopAsync(capacity: 1, startsAt: new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));
        var c = await _h.AddWorkshopAsync(capacity: 1, startsAt: new DateTimeOffset(2026, 12, 1, 11, 0, 0, TimeSpan.Zero));
        var ada = await _h.AddParticipantAsync("Ada");
        var bjorn = await _h.AddParticipantAsync("Bjørn");
        var chen = await _h.AddParticipantAsync("Chen");

        var adaSeat = await _h.Registrations.RegisterAsync(a, ada, null);
        var bjornWaitlisted = await _h.Registrations.RegisterAsync(a, bjorn, null);
        var chenWaitlisted = await _h.Registrations.RegisterAsync(a, chen, null);
        var bjornInC = await _h.Registrations.RegisterAsync(c, bjorn, null);
        Assert.Equal(RegistrationStatus.Confirmed, bjornInC.Registration.Status);

        await _h.Registrations.CancelAsync(adaSeat.Registration.Id);

        Assert.Equal(RegistrationStatus.Waitlisted, await _h.StatusOfAsync(bjornWaitlisted.Registration.Id));
        Assert.Equal(RegistrationStatus.Confirmed, await _h.StatusOfAsync(chenWaitlisted.Registration.Id));
        Assert.Equal(1, await _h.CountRegistrationsAsync(a, RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Registration_is_rejected_when_it_overlaps_a_confirmed_workshop()
    {
        var morning = await _h.AddWorkshopAsync(capacity: 5, startsAt: new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));
        var overlapping = await _h.AddWorkshopAsync(capacity: 5, startsAt: new DateTimeOffset(2026, 12, 1, 11, 0, 0, TimeSpan.Zero));
        var participant = await _h.AddParticipantAsync("Dina");
        await _h.Registrations.RegisterAsync(morning, participant, null);

        var ex = await Assert.ThrowsAsync<SeatFlowException>(() =>
            _h.Registrations.RegisterAsync(overlapping, participant, null));

        Assert.Equal(SeatFlowErrorKind.Conflict, ex.Kind);
        Assert.Equal(0, await _h.CountRegistrationsAsync(overlapping, RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Back_to_back_workshops_do_not_count_as_overlapping()
    {
        // End times are exclusive: 10:00-12:00 and 12:00-14:00 can both be confirmed.
        var first = await _h.AddWorkshopAsync(capacity: 5, startsAt: new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));
        var next = await _h.AddWorkshopAsync(capacity: 5, startsAt: new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero));
        var participant = await _h.AddParticipantAsync("Erik");

        await _h.Registrations.RegisterAsync(first, participant, null);
        var result = await _h.Registrations.RegisterAsync(next, participant, null);

        Assert.Equal(RegistrationStatus.Confirmed, result.Registration.Status);
    }

    [Fact]
    public async Task Participant_cannot_register_twice_for_the_same_workshop_while_active()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 5);
        var participant = await _h.AddParticipantAsync("Fatima");
        await _h.Registrations.RegisterAsync(workshop, participant, null);

        var ex = await Assert.ThrowsAsync<SeatFlowException>(() =>
            _h.Registrations.RegisterAsync(workshop, participant, null));

        Assert.Equal(SeatFlowErrorKind.Conflict, ex.Kind);
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Participant_can_register_again_after_a_previous_registration_was_cancelled()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 5);
        var participant = await _h.AddParticipantAsync("Gunnar");
        var first = await _h.Registrations.RegisterAsync(workshop, participant, null);
        await _h.Registrations.CancelAsync(first.Registration.Id);

        var second = await _h.Registrations.RegisterAsync(workshop, participant, null);

        Assert.NotEqual(first.Registration.Id, second.Registration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, second.Registration.Status);
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent_and_does_not_promote_twice()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 1);
        var confirmed = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Hanne"), null);
        var waiting1 = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Ivar"), null);
        var waiting2 = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Jonas"), null);

        var first = await _h.Registrations.CancelAsync(confirmed.Registration.Id);
        var second = await _h.Registrations.CancelAsync(confirmed.Registration.Id);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Null(second.Promoted);
        Assert.Equal(RegistrationStatus.Cancelled, second.Registration.Status);
        Assert.Equal(RegistrationStatus.Confirmed, await _h.StatusOfAsync(waiting1.Registration.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, await _h.StatusOfAsync(waiting2.Registration.Id));
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Confirmed));
        // Exactly one cancellation notice and one promotion notice, from the first cancel only.
        Assert.Equal(1, _h.Recorder.Sent.Count(n => n.Kind == NotificationKind.RegistrationCancelled));
        Assert.Equal(1, _h.Recorder.Sent.Count(n => n.Kind == NotificationKind.WaitlistPromoted));
    }

    [Fact]
    public async Task Cancelling_a_waitlisted_registration_frees_no_seat_and_promotes_nobody()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 1);
        await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Kari"), null);
        var waiting = await _h.Registrations.RegisterAsync(workshop, await _h.AddParticipantAsync("Lars"), null);

        var cancel = await _h.Registrations.CancelAsync(waiting.Registration.Id);

        Assert.True(cancel.Changed);
        Assert.Null(cancel.Promoted);
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Cancelling_an_unknown_registration_is_not_found()
    {
        var ex = await Assert.ThrowsAsync<SeatFlowException>(() => _h.Registrations.CancelAsync(9999));

        Assert.Equal(SeatFlowErrorKind.NotFound, ex.Kind);
    }

    [Fact]
    public async Task Unknown_workshop_or_participant_is_not_found()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 1);
        var participant = await _h.AddParticipantAsync("Mia");

        var missingWorkshop = await Assert.ThrowsAsync<SeatFlowException>(() =>
            _h.Registrations.RegisterAsync(9999, participant, null));
        var missingParticipant = await Assert.ThrowsAsync<SeatFlowException>(() =>
            _h.Registrations.RegisterAsync(workshop, 9999, null));

        Assert.Equal(SeatFlowErrorKind.NotFound, missingWorkshop.Kind);
        Assert.Equal(SeatFlowErrorKind.NotFound, missingParticipant.Kind);
    }

    [Fact]
    public async Task Notification_failure_does_not_roll_back_a_registration_or_a_cancellation()
    {
        using var failing = new SeatFlowHarness(new FailingNotifications());
        var workshop = await failing.AddWorkshopAsync(capacity: 1);
        var ada = await failing.Registrations.RegisterAsync(workshop, await failing.AddParticipantAsync("Ada"), null);
        var bjorn = await failing.Registrations.RegisterAsync(workshop, await failing.AddParticipantAsync("Bjørn"), null);

        var cancel = await failing.Registrations.CancelAsync(ada.Registration.Id);

        Assert.Equal(RegistrationStatus.Confirmed, ada.Registration.Status);
        Assert.Equal(RegistrationStatus.Cancelled, cancel.Registration.Status);
        Assert.Equal(bjorn.Registration.Id, cancel.Promoted?.Id);
        Assert.Equal(RegistrationStatus.Confirmed, await failing.StatusOfAsync(bjorn.Registration.Id));
    }
}
