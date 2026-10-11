using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Services;

public class RegistrationService
{
    private readonly SeatFlowContext _context;
    private readonly INotificationService _notificationService;
    private static readonly Dictionary<string, int> IdempotencyKeyMap = new();

    public RegistrationService(SeatFlowContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<Registration> RegisterAsync(int workshopId, int participantId, string? idempotencyKey = null)
    {
        // Handle idempotency
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            if (IdempotencyKeyMap.TryGetValue(idempotencyKey, out var registrationId))
            {
                var existing = await _context.Registrations.FindAsync(registrationId);
                if (existing != null)
                    return existing;
            }
        }

        var workshop = await _context.Workshops.Include(w => w.Registrations)
            .FirstOrDefaultAsync(w => w.Id == workshopId);
        if (workshop == null)
            throw new InvalidOperationException("Workshop not found");

        var participant = await _context.Participants.FindAsync(participantId);
        if (participant == null)
            throw new InvalidOperationException("Participant not found");

        // Check if participant already has a non-cancelled registration for this workshop
        var existingReg = await _context.Registrations
            .FirstOrDefaultAsync(r => r.WorkshopId == workshopId && r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled);
        if (existingReg != null)
            throw new InvalidOperationException("Participant already registered for this workshop");

        // Check for schedule conflicts
        var confirmedRegistrations = await _context.Registrations
            .Where(r => r.ParticipantId == participantId && r.Status == RegistrationStatus.Confirmed)
            .Include(r => r.Workshop)
            .ToListAsync();

        if (confirmedRegistrations.Any(r => WorkshopsOverlap(r.Workshop, workshop)))
            throw new InvalidOperationException("Schedule conflict detected");

        // Determine if confirmed or waitlisted
        var confirmedCount = workshop.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed);
        var status = confirmedCount < workshop.Capacity ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted;

        var registration = new Registration
        {
            WorkshopId = workshopId,
            ParticipantId = participantId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        _context.Registrations.Add(registration);
        await _context.SaveChangesAsync();

        // Store idempotency key
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            IdempotencyKeyMap[idempotencyKey] = registration.Id;
        }

        // Send notification
        if (status == RegistrationStatus.Confirmed)
        {
            await _notificationService.SendRegistrationConfirmedAsync(registration);
        }

        return registration;
    }

    public async Task CancelAsync(int registrationId)
    {
        var registration = await _context.Registrations
            .Include(r => r.Workshop)
            .ThenInclude(w => w.Registrations)
            .FirstOrDefaultAsync(r => r.Id == registrationId);

        if (registration == null)
            throw new InvalidOperationException("Registration not found");

        // Idempotent: if already cancelled, just return
        if (registration.Status == RegistrationStatus.Cancelled)
            return;

        var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
        registration.Status = RegistrationStatus.Cancelled;
        await _context.SaveChangesAsync();

        await _notificationService.SendRegistrationCancelledAsync(registration);

        // Try to promote waitlisted participant
        if (wasConfirmed)
        {
            await PromoteFromWaitlistAsync(registration.WorkshopId);
        }
    }

    private async Task PromoteFromWaitlistAsync(int workshopId)
    {
        var workshop = await _context.Workshops.Include(w => w.Registrations)
            .FirstOrDefaultAsync(w => w.Id == workshopId);
        if (workshop == null)
            return;

        // Get oldest waitlisted registration
        var waitlisted = await _context.Registrations
            .Where(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        foreach (var reg in waitlisted)
        {
            // Check if this participant would have a conflict
            var confirmedRegs = await _context.Registrations
                .Where(r => r.ParticipantId == reg.ParticipantId && r.Status == RegistrationStatus.Confirmed)
                .Include(r => r.Workshop)
                .ToListAsync();

            var hasConflict = confirmedRegs.Any(r => WorkshopsOverlap(r.Workshop, workshop));

            if (!hasConflict)
            {
                // Promote
                reg.Status = RegistrationStatus.Confirmed;
                await _context.SaveChangesAsync();
                await _notificationService.SendWaitlistPromotedAsync(reg);
                return;
            }
            // else: leave on waitlist and try next
        }
    }

    private static bool WorkshopsOverlap(Workshop w1, Workshop w2)
    {
        return w1.Id != w2.Id && w1.StartTime < w2.EndTime && w2.StartTime < w1.EndTime;
    }

    public async Task<List<Registration>> GetWorkshopRegistrationsAsync(int workshopId)
    {
        return await _context.Registrations
            .Where(r => r.WorkshopId == workshopId)
            .Include(r => r.Participant)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Registration>> GetParticipantScheduleAsync(int participantId)
    {
        return await _context.Registrations
            .Where(r => r.ParticipantId == participantId && r.Status == RegistrationStatus.Confirmed)
            .Include(r => r.Workshop)
            .OrderBy(r => r.Workshop.StartTime)
            .ToListAsync();
    }
}
