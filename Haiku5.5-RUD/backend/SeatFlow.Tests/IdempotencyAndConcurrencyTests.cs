using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public class IdempotencyAndConcurrencyTests : IDisposable
{
    private readonly SeatFlowHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Repeating_a_request_with_the_same_idempotency_key_returns_the_same_registration()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 2);
        var participant = await _h.AddParticipantAsync("Ada");

        var first = await _h.Registrations.RegisterAsync(workshop, participant, "key-123");
        var retry = await _h.Registrations.RegisterAsync(workshop, participant, "key-123");

        Assert.False(first.Replayed);
        Assert.True(retry.Replayed);
        Assert.Equal(first.Registration.Id, retry.Registration.Id);
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Confirmed));
        Assert.Single(_h.Recorder.Sent, n => n.Kind == NotificationKind.RegistrationConfirmed);
    }

    [Fact]
    public async Task A_retry_with_the_same_key_does_not_take_a_second_seat_or_waitlist_slot()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 1);
        var ada = await _h.AddParticipantAsync("Ada");
        var bjorn = await _h.AddParticipantAsync("Bjørn");
        await _h.Registrations.RegisterAsync(workshop, ada, null);

        var waitlisted = await _h.Registrations.RegisterAsync(workshop, bjorn, "waitlist-key");
        var retry = await _h.Registrations.RegisterAsync(workshop, bjorn, "waitlist-key");

        Assert.Equal(waitlisted.Registration.Id, retry.Registration.Id);
        Assert.Equal(RegistrationStatus.Waitlisted, retry.Registration.Status);
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Waitlisted));
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_registration_is_rejected()
    {
        var workshopA = await _h.AddWorkshopAsync(capacity: 2);
        var workshopB = await _h.AddWorkshopAsync(capacity: 2, startsAt: new DateTimeOffset(2026, 12, 2, 10, 0, 0, TimeSpan.Zero));
        var participant = await _h.AddParticipantAsync("Ada");
        await _h.Registrations.RegisterAsync(workshopA, participant, "shared-key");

        var ex = await Assert.ThrowsAsync<SeatFlowException>(() =>
            _h.Registrations.RegisterAsync(workshopB, participant, "shared-key"));

        Assert.Equal(SeatFlowErrorKind.Conflict, ex.Kind);
    }

    [Fact]
    public async Task Concurrent_attempts_for_the_final_seat_confirm_exactly_one_participant()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 1);
        var participants = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            participants.Add(await _h.AddParticipantAsync($"Racer {i}"));
        }

        var attempts = participants.Select(p => _h.Registrations.RegisterAsync(workshop, p, null));
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r.Registration.Status == RegistrationStatus.Confirmed));
        Assert.Equal(9, results.Count(r => r.Registration.Status == RegistrationStatus.Waitlisted));
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Concurrent_attempts_with_the_same_idempotency_key_create_a_single_registration()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 5);
        var participant = await _h.AddParticipantAsync("Ada");

        var attempts = Enumerable.Range(0, 5).Select(_ => _h.Registrations.RegisterAsync(workshop, participant, "same-key"));
        var results = await Task.WhenAll(attempts);

        Assert.Single(results.Select(r => r.Registration.Id).Distinct());
        Assert.Equal(1, results.Count(r => !r.Replayed));
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Concurrent_duplicate_attempts_from_one_participant_leave_only_one_active_registration()
    {
        var workshop = await _h.AddWorkshopAsync(capacity: 5);
        var participant = await _h.AddParticipantAsync("Bjørn");

        async Task<RegistrationResult?> Attempt(int i)
        {
            try
            {
                return await _h.Registrations.RegisterAsync(workshop, participant, $"distinct-key-{i}");
            }
            catch (SeatFlowException ex) when (ex.Kind == SeatFlowErrorKind.Conflict)
            {
                return null;
            }
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(Attempt));

        Assert.Equal(1, results.Count(r => r is not null));
        Assert.Equal(1, await _h.CountRegistrationsAsync(workshop, RegistrationStatus.Confirmed));
    }
}
