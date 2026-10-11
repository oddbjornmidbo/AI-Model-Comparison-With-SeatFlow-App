using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Models;
using SeatFlow.Api.Services;
using Xunit;

namespace SeatFlow.Tests;

public sealed class RegistrationBehaviorTests
{
    [Fact]
    public async Task Available_seat_is_confirmed_and_sends_confirmation()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync();
        var participant = await database.AddParticipantAsync("Ada");
        var notifications = new RecordingNotifications();
        await using var context = database.CreateContext();

        var registration = await database.CreateService(context, notifications)
            .RegisterAsync(workshop.Id, participant.Id, null);

        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        Assert.Equal(participant.Id, registration.ParticipantId);
        Assert.Equal("Ada", registration.ParticipantName);
        Assert.True(registration.Id > 0);
        var notification = Assert.Single(notifications.Sent);
        Assert.Equal(NotificationKind.Confirmed, notification.Kind);
        Assert.Equal(registration.Id, notification.RegistrationId);
        await using var verification = database.CreateContext();
        Assert.Equal(RegistrationStatus.Confirmed, (await verification.Registrations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Full_workshop_puts_new_participant_on_waitlist_without_confirmation()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var first = await database.AddParticipantAsync();
        var second = await database.AddParticipantAsync();
        var notifications = new RecordingNotifications();
        await using var context = database.CreateContext();
        var service = database.CreateService(context, notifications);
        await service.RegisterAsync(workshop.Id, first.Id, null);

        var registration = await service.RegisterAsync(workshop.Id, second.Id, null);

        Assert.Equal(RegistrationStatus.Waitlisted, registration.Status);
        Assert.Single(notifications.Sent);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
        Assert.Equal(1, await verification.Registrations.CountAsync(r => r.Status == RegistrationStatus.Waitlisted));
    }

    [Fact]
    public async Task Cancelling_confirmed_registration_promotes_oldest_waitlisted_registration()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var owner = await database.AddParticipantAsync();
        var older = await database.AddParticipantAsync();
        var newer = await database.AddParticipantAsync();
        var notifications = new RecordingNotifications();
        await using var context = database.CreateContext();
        var service = database.CreateService(context, notifications);
        var confirmed = await service.RegisterAsync(workshop.Id, owner.Id, null);
        var newerRegistration = await service.RegisterAsync(workshop.Id, newer.Id, null);
        var olderRegistration = await service.RegisterAsync(workshop.Id, older.Id, null);

        // Deliberately make creation order differ from ID order: FIFO is based on the timestamp.
        var olderEntity = await context.Registrations.SingleAsync(r => r.Id == olderRegistration.Id);
        var newerEntity = await context.Registrations.SingleAsync(r => r.Id == newerRegistration.Id);
        olderEntity.CreatedAt = new DateTime(2030, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        newerEntity.CreatedAt = olderEntity.CreatedAt.AddMinutes(1);
        await context.SaveChangesAsync();

        var cancellation = await service.CancelAsync(confirmed.Id);

        Assert.Equal(RegistrationStatus.Cancelled, cancellation.Status);
        await using var verification = database.CreateContext();
        Assert.Equal(RegistrationStatus.Confirmed,
            (await verification.Registrations.SingleAsync(r => r.Id == olderRegistration.Id)).Status);
        Assert.Equal(RegistrationStatus.Waitlisted,
            (await verification.Registrations.SingleAsync(r => r.Id == newerRegistration.Id)).Status);
        Assert.Contains(notifications.Sent, n => n.Kind == NotificationKind.Cancelled && n.RegistrationId == confirmed.Id);
        Assert.Contains(notifications.Sent, n => n.Kind == NotificationKind.Promoted && n.RegistrationId == olderRegistration.Id);
        Assert.Equal(1, await verification.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Promotion_skips_conflicting_participant_and_leaves_them_waitlisted()
    {
        await using var database = await CreateDatabaseAsync();
        var fullWorkshop = await database.AddWorkshopAsync(capacity: 1);
        var overlappingWorkshop = await database.AddWorkshopAsync(startHour: 9, startMinute: 30, endHour: 10, endMinute: 30);
        var owner = await database.AddParticipantAsync();
        var conflicting = await database.AddParticipantAsync();
        var eligible = await database.AddParticipantAsync();
        var notifications = new RecordingNotifications();
        await using var context = database.CreateContext();
        var service = database.CreateService(context, notifications);
        var confirmed = await service.RegisterAsync(fullWorkshop.Id, owner.Id, null);
        await service.RegisterAsync(overlappingWorkshop.Id, conflicting.Id, null);
        var firstWaitlisted = await service.RegisterAsync(fullWorkshop.Id, conflicting.Id, null);
        var secondWaitlisted = await service.RegisterAsync(fullWorkshop.Id, eligible.Id, null);

        await service.CancelAsync(confirmed.Id);

        await using var verification = database.CreateContext();
        Assert.Equal(RegistrationStatus.Waitlisted,
            (await verification.Registrations.SingleAsync(r => r.Id == firstWaitlisted.Id)).Status);
        Assert.Equal(RegistrationStatus.Confirmed,
            (await verification.Registrations.SingleAsync(r => r.Id == secondWaitlisted.Id)).Status);
        Assert.DoesNotContain(notifications.Sent, n => n.Kind == NotificationKind.Promoted && n.ParticipantId == conflicting.Id);
        Assert.Contains(notifications.Sent, n => n.Kind == NotificationKind.Promoted && n.ParticipantId == eligible.Id);
    }

    [Fact]
    public async Task If_every_waitlisted_participant_conflicts_the_seat_remains_open()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var overlapping = await database.AddWorkshopAsync();
        var owner = await database.AddParticipantAsync();
        var conflicting = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context);
        var confirmed = await service.RegisterAsync(workshop.Id, owner.Id, null);
        await service.RegisterAsync(overlapping.Id, conflicting.Id, null);
        var waiting = await service.RegisterAsync(workshop.Id, conflicting.Id, null);

        await service.CancelAsync(confirmed.Id);

        await using var verification = database.CreateContext();
        Assert.Equal(0, await verification.Registrations.CountAsync(r => r.WorkshopId == workshop.Id && r.Status == RegistrationStatus.Confirmed));
        Assert.Equal(RegistrationStatus.Waitlisted,
            (await verification.Registrations.SingleAsync(r => r.Id == waiting.Id)).Status);
    }

    [Theory]
    [InlineData(9, 30, 10, 30)]
    [InlineData(9, 0, 10, 0)]
    [InlineData(8, 30, 10, 30)]
    [InlineData(9, 15, 9, 45)]
    public async Task Overlapping_workshop_cannot_be_confirmed(int startHour, int startMinute, int endHour, int endMinute)
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync();
        var overlapping = await database.AddWorkshopAsync(startHour: startHour, startMinute: startMinute,
            endHour: endHour, endMinute: endMinute);
        var participant = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context);
        await service.RegisterAsync(workshop.Id, participant.Id, null);

        var error = await Assert.ThrowsAsync<ApiException>(() => service.RegisterAsync(overlapping.Id, participant.Id, null));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("ScheduleConflict", error.Code);
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Registrations.ToListAsync());
    }

    [Fact]
    public async Task Back_to_back_workshops_can_both_be_confirmed()
    {
        await using var database = await CreateDatabaseAsync();
        var first = await database.AddWorkshopAsync();
        var next = await database.AddWorkshopAsync(startHour: 10, endHour: 11);
        var participant = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context);
        await service.RegisterAsync(first.Id, participant.Id, null);

        var registration = await service.RegisterAsync(next.Id, participant.Id, null);

        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        await using var verification = database.CreateContext();
        Assert.Equal(2, await verification.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_active_registration_cannot_be_duplicated(bool waitlisted)
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var participant = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context);
        if (waitlisted)
        {
            var owner = await database.AddParticipantAsync();
            await service.RegisterAsync(workshop.Id, owner.Id, null);
        }
        await service.RegisterAsync(workshop.Id, participant.Id, null);

        var error = await Assert.ThrowsAsync<ApiException>(() => service.RegisterAsync(workshop.Id, participant.Id, null));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("DuplicateRegistration", error.Code);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Registrations.CountAsync(r => r.ParticipantId == participant.Id));
    }

    [Fact]
    public async Task Participant_can_register_again_after_cancellation()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync();
        var participant = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context);
        var original = await service.RegisterAsync(workshop.Id, participant.Id, null);
        await service.CancelAsync(original.Id);

        var replacement = await service.RegisterAsync(workshop.Id, participant.Id, null);

        Assert.NotEqual(original.Id, replacement.Id);
        Assert.Equal(RegistrationStatus.Confirmed, replacement.Status);
        await using var verification = database.CreateContext();
        Assert.Equal(2, await verification.Registrations.CountAsync());
        Assert.Equal(1, await verification.Registrations.CountAsync(r => r.Status == RegistrationStatus.Cancelled));
    }

    [Fact]
    public async Task Repeated_idempotency_key_returns_same_registration_without_second_notification()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync();
        var participant = await database.AddParticipantAsync();
        var notifications = new RecordingNotifications();
        await using var firstContext = database.CreateContext();
        var original = await database.CreateService(firstContext, notifications)
            .RegisterAsync(workshop.Id, participant.Id, "retry-key");
        await using var secondContext = database.CreateContext();

        var repeated = await database.CreateService(secondContext, notifications)
            .RegisterAsync(workshop.Id, participant.Id, "retry-key");

        Assert.Equal(original, repeated);
        Assert.Single(notifications.Sent);
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Registrations.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Idempotency_key_cannot_be_reused_for_a_different_request(bool changeParticipant)
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync();
        var otherWorkshop = await database.AddWorkshopAsync(startHour: 11, endHour: 12);
        var participant = await database.AddParticipantAsync();
        var otherParticipant = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context);
        await service.RegisterAsync(workshop.Id, participant.Id, "shared-key");

        var error = await Assert.ThrowsAsync<ApiException>(() => service.RegisterAsync(
            changeParticipant ? workshop.Id : otherWorkshop.Id,
            changeParticipant ? otherParticipant.Id : participant.Id,
            "shared-key"));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("IdempotencyKeyConflict", error.Code);
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Registrations.ToListAsync());
    }

    [Fact]
    public async Task Cancelling_twice_does_not_promote_twice_or_send_duplicate_cancellation()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var owner = await database.AddParticipantAsync();
        var first = await database.AddParticipantAsync();
        var second = await database.AddParticipantAsync();
        var notifications = new RecordingNotifications();
        await using var context = database.CreateContext();
        var service = database.CreateService(context, notifications);
        var confirmed = await service.RegisterAsync(workshop.Id, owner.Id, null);
        var firstWaiting = await service.RegisterAsync(workshop.Id, first.Id, null);
        var secondWaiting = await service.RegisterAsync(workshop.Id, second.Id, null);

        var firstCancellation = await service.CancelAsync(confirmed.Id);
        var secondCancellation = await service.CancelAsync(confirmed.Id);

        Assert.Equal(firstCancellation, secondCancellation);
        Assert.Single(notifications.Sent, n => n.Kind == NotificationKind.Cancelled);
        Assert.Single(notifications.Sent, n => n.Kind == NotificationKind.Promoted);
        await using var verification = database.CreateContext();
        Assert.Equal(RegistrationStatus.Confirmed,
            (await verification.Registrations.SingleAsync(r => r.Id == firstWaiting.Id)).Status);
        Assert.Equal(RegistrationStatus.Waitlisted,
            (await verification.Registrations.SingleAsync(r => r.Id == secondWaiting.Id)).Status);
    }

    [Fact]
    public async Task Cancelling_waitlisted_registration_does_not_free_a_confirmed_seat()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var owner = await database.AddParticipantAsync();
        var waiting = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context);
        var confirmed = await service.RegisterAsync(workshop.Id, owner.Id, null);
        var waitlisted = await service.RegisterAsync(workshop.Id, waiting.Id, null);

        await service.CancelAsync(waitlisted.Id);

        await using var verification = database.CreateContext();
        Assert.Equal(RegistrationStatus.Confirmed,
            (await verification.Registrations.SingleAsync(r => r.Id == confirmed.Id)).Status);
        Assert.Equal(RegistrationStatus.Cancelled,
            (await verification.Registrations.SingleAsync(r => r.Id == waitlisted.Id)).Status);
    }

    [Fact]
    public async Task Notification_failure_does_not_undo_confirmation_cancellation_or_promotion()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var owner = await database.AddParticipantAsync();
        var waiting = await database.AddParticipantAsync();
        await using var context = database.CreateContext();
        var service = database.CreateService(context, new FailingNotifications());
        var confirmed = await service.RegisterAsync(workshop.Id, owner.Id, null);
        var waitlisted = await service.RegisterAsync(workshop.Id, waiting.Id, null);

        var cancellation = await service.CancelAsync(confirmed.Id);

        Assert.Equal(RegistrationStatus.Cancelled, cancellation.Status);
        await using var verification = database.CreateContext();
        Assert.Equal(RegistrationStatus.Cancelled,
            (await verification.Registrations.SingleAsync(r => r.Id == confirmed.Id)).Status);
        Assert.Equal(RegistrationStatus.Confirmed,
            (await verification.Registrations.SingleAsync(r => r.Id == waitlisted.Id)).Status);
    }

    [Fact]
    public async Task Simultaneous_attempts_from_separate_connections_share_final_seat_safely()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync(capacity: 1);
        var first = await database.AddParticipantAsync();
        var second = await database.AddParticipantAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<SeatFlow.Api.Contracts.RegistrationDto> AttemptAsync(int participantId)
        {
            await using var context = database.CreateContext();
            if (Interlocked.Increment(ref readyCount) == 2) ready.SetResult();
            await start.Task;
            return await database.CreateService(context).RegisterAsync(workshop.Id, participantId, null);
        }

        var firstAttempt = Task.Run(() => AttemptAsync(first.Id));
        var secondAttempt = Task.Run(() => AttemptAsync(second.Id));
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        start.SetResult();
        var results = await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Single(results, r => r.Status == RegistrationStatus.Confirmed);
        Assert.Single(results, r => r.Status == RegistrationStatus.Waitlisted);
        await using var verification = database.CreateContext();
        Assert.Equal(2, await verification.Registrations.CountAsync());
        Assert.Equal(1, await verification.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Simultaneous_overlapping_requests_for_same_participant_confirm_only_one()
    {
        await using var database = await CreateDatabaseAsync();
        var firstWorkshop = await database.AddWorkshopAsync();
        var secondWorkshop = await database.AddWorkshopAsync(startHour: 9, startMinute: 30, endHour: 10, endMinute: 30);
        var participant = await database.AddParticipantAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string> AttemptAsync(int workshopId)
        {
            await using var context = database.CreateContext();
            if (Interlocked.Increment(ref readyCount) == 2) ready.SetResult();
            await start.Task;
            try
            {
                await database.CreateService(context).RegisterAsync(workshopId, participant.Id, null);
                return "Confirmed";
            }
            catch (ApiException error)
            {
                return error.Code;
            }
        }

        var firstAttempt = Task.Run(() => AttemptAsync(firstWorkshop.Id));
        var secondAttempt = Task.Run(() => AttemptAsync(secondWorkshop.Id));
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        start.SetResult();
        var results = await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Contains("Confirmed", results);
        Assert.Contains("ScheduleConflict", results);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Simultaneous_retries_with_same_key_create_one_registration()
    {
        await using var database = await CreateDatabaseAsync();
        var workshop = await database.AddWorkshopAsync();
        var participant = await database.AddParticipantAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> AttemptAsync()
        {
            await using var context = database.CreateContext();
            if (Interlocked.Increment(ref readyCount) == 2) ready.SetResult();
            await start.Task;
            var registration = await database.CreateService(context).RegisterAsync(workshop.Id, participant.Id, "parallel-key");
            return registration.Id;
        }

        var firstAttempt = Task.Run(AttemptAsync);
        var secondAttempt = Task.Run(AttemptAsync);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        start.SetResult();
        var results = await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Equal(results[0], results[1]);
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Registrations.ToListAsync());
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var database = new TestDatabase();
        await database.InitializeAsync();
        return database;
    }
}
