using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public class RegistrationServiceTests : IDisposable
{
    private readonly SeatFlowContext _context;
    private readonly InMemoryNotificationService _notificationService;
    private readonly RegistrationService _service;

    public RegistrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<SeatFlowContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;

        _context = new SeatFlowContext(options);
        var loggerFactory = new LoggerFactory();
        var logger = loggerFactory.CreateLogger<InMemoryNotificationService>();
        _notificationService = new InMemoryNotificationService(logger);
        _service = new RegistrationService(_context, _notificationService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task SetupAsync()
    {
        var w1 = new Workshop
        {
            Title = "Workshop 1",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            Capacity = 2
        };
        var p1 = new Participant { Name = "Alice", Email = "alice@test.com" };
        var p2 = new Participant { Name = "Bob", Email = "bob@test.com" };
        var p3 = new Participant { Name = "Carol", Email = "carol@test.com" };

        _context.Workshops.Add(w1);
        _context.Participants.AddRange(p1, p2, p3);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task Register_WithAvailableCapacity_ReturnsConfirmed()
    {
        await SetupAsync();

        var reg = await _service.RegisterAsync(1, 1);

        Assert.NotNull(reg);
        Assert.Equal(RegistrationStatus.Confirmed, reg.Status);
        Assert.Equal(1, reg.WorkshopId);
        Assert.Equal(1, reg.ParticipantId);
    }

    [Fact]
    public async Task Register_WhenFull_ReturnsWaitlisted()
    {
        await SetupAsync();

        await _service.RegisterAsync(1, 1);
        await _service.RegisterAsync(1, 2);
        var reg3 = await _service.RegisterAsync(1, 3);

        Assert.Equal(RegistrationStatus.Waitlisted, reg3.Status);
    }

    [Fact]
    public async Task Register_DuplicateForSameWorkshop_Throws()
    {
        await SetupAsync();

        await _service.RegisterAsync(1, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(1, 1));
    }

    [Fact]
    public async Task Register_WithIdempotencyKey_ReturnsSameRegistration()
    {
        await SetupAsync();

        var reg1 = await _service.RegisterAsync(1, 1, "key-123");
        var reg2 = await _service.RegisterAsync(1, 2, "key-123");

        Assert.Equal(reg1.Id, reg2.Id);
        Assert.Equal(1, reg2.ParticipantId);
    }

    [Fact]
    public async Task Cancel_WhenConfirmed_PromotesFromWaitlist()
    {
        await SetupAsync();
        var add_w2 = new Workshop
        {
            Title = "Workshop 2",
            StartTime = DateTime.UtcNow.AddHours(5),
            EndTime = DateTime.UtcNow.AddHours(6),
            Capacity = 1
        };
        _context.Workshops.Add(add_w2);
        await _context.SaveChangesAsync();

        var reg1 = await _service.RegisterAsync(1, 1);
        var reg2 = await _service.RegisterAsync(1, 2);
        var reg3 = await _service.RegisterAsync(1, 3);

        Assert.Equal(RegistrationStatus.Confirmed, reg1.Status);
        Assert.Equal(RegistrationStatus.Confirmed, reg2.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, reg3.Status);

        await _service.CancelAsync(reg1.Id);

        await _context.Entry(reg3).ReloadAsync();
        Assert.Equal(RegistrationStatus.Confirmed, reg3.Status);
    }

    [Fact]
    public async Task Cancel_Idempotent_CanCancelTwice()
    {
        await SetupAsync();

        var reg = await _service.RegisterAsync(1, 1);
        await _service.CancelAsync(reg.Id);
        await _service.CancelAsync(reg.Id);

        var reloaded = await _context.Registrations.FindAsync(reg.Id);
        Assert.Equal(RegistrationStatus.Cancelled, reloaded!.Status);
    }

    [Fact]
    public async Task Register_WithScheduleConflict_Throws()
    {
        var now = DateTime.UtcNow;
        var w1 = new Workshop
        {
            Title = "W1",
            StartTime = now.AddHours(1),
            EndTime = now.AddHours(3),
            Capacity = 2
        };
        var w2 = new Workshop
        {
            Title = "W2",
            StartTime = now.AddHours(2),
            EndTime = now.AddHours(4),
            Capacity = 2
        };
        _context.Workshops.AddRange(w1, w2);
        var p = new Participant { Name = "Alice", Email = "a@t.com" };
        _context.Participants.Add(p);
        await _context.SaveChangesAsync();

        await _service.RegisterAsync(1, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(2, 1));
    }

    [Fact]
    public async Task PromoteFromWaitlist_SkipsParticipantWithConflict()
    {
        var now = DateTime.UtcNow;
        var w1 = new Workshop { Title = "W1", StartTime = now.AddHours(1), EndTime = now.AddHours(2), Capacity = 1 };
        var w2 = new Workshop { Title = "W2", StartTime = now.AddHours(1.5), EndTime = now.AddHours(2.5), Capacity = 10 };

        var p1 = new Participant { Name = "P1", Email = "p1@t.com" };
        var p2 = new Participant { Name = "P2", Email = "p2@t.com" };
        var p3 = new Participant { Name = "P3", Email = "p3@t.com" };

        _context.Workshops.AddRange(w1, w2);
        _context.Participants.AddRange(p1, p2, p3);
        await _context.SaveChangesAsync();

        var reg1 = await _service.RegisterAsync(1, 1);
        var reg2 = await _service.RegisterAsync(1, 2);
        var reg3 = await _service.RegisterAsync(1, 3);

        await _service.RegisterAsync(2, 2);

        await _service.CancelAsync(reg1.Id);

        await _context.Entry(reg2).ReloadAsync();
        await _context.Entry(reg3).ReloadAsync();

        Assert.Equal(RegistrationStatus.Cancelled, reg1.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, reg2.Status);
        Assert.Equal(RegistrationStatus.Confirmed, reg3.Status);
    }

    [Fact]
    public async Task GetParticipantSchedule_ReturnsConfirmedOnly()
    {
        var now = DateTime.UtcNow;
        var w1 = new Workshop { Title = "W1", StartTime = now.AddHours(1), EndTime = now.AddHours(2), Capacity = 2 };
        var w2 = new Workshop { Title = "W2", StartTime = now.AddHours(3), EndTime = now.AddHours(4), Capacity = 2 };

        var p1 = new Participant { Name = "P1", Email = "p1@t.com" };
        _context.Workshops.AddRange(w1, w2);
        _context.Participants.Add(p1);
        await _context.SaveChangesAsync();

        var reg1 = await _service.RegisterAsync(1, 1);
        var reg2 = await _service.RegisterAsync(2, 1);

        var schedule = await _service.GetParticipantScheduleAsync(1);

        Assert.Equal(2, schedule.Count);
        Assert.All(schedule, r => Assert.Equal(RegistrationStatus.Confirmed, r.Status));
    }

    [Fact]
    public async Task FIFOPromotion_PromotesOldestEligible()
    {
        var now = DateTime.UtcNow;
        var w1 = new Workshop { Title = "W1", StartTime = now.AddHours(1), EndTime = now.AddHours(2), Capacity = 1 };
        var p1 = new Participant { Name = "P1", Email = "p1@t.com" };
        var p2 = new Participant { Name = "P2", Email = "p2@t.com" };
        var p3 = new Participant { Name = "P3", Email = "p3@t.com" };

        _context.Workshops.Add(w1);
        _context.Participants.AddRange(p1, p2, p3);
        await _context.SaveChangesAsync();

        var reg1 = await _service.RegisterAsync(1, 1);
        var reg2 = await _service.RegisterAsync(1, 2);
        var reg3 = await _service.RegisterAsync(1, 3);

        Assert.True(reg2.CreatedAt <= reg3.CreatedAt);

        await _service.CancelAsync(reg1.Id);

        await _context.Entry(reg2).ReloadAsync();
        Assert.Equal(RegistrationStatus.Confirmed, reg2.Status);

        await _context.Entry(reg3).ReloadAsync();
        Assert.Equal(RegistrationStatus.Waitlisted, reg3.Status);
    }
}
