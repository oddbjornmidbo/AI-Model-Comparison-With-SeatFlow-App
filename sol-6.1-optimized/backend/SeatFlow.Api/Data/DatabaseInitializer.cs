using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(SeatFlowDbContext db, bool seed = true,
        CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        // EnsureCreated does not add tables to an existing database. This additive
        // upgrade retains workshops, registrations and their original retry keys.
        await db.Database.ExecuteSqlRawAsync(WorkshopCreationSchema, cancellationToken);

        // These backstops protect the invariants even if a writer bypasses the application service.
        await db.Database.ExecuteSqlRawAsync(Triggers, cancellationToken);
        if (!seed) return;

        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await using var sqliteTransaction = connection.BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction, cancellationToken)
            ?? throw new InvalidOperationException("Unable to enlist the SQLite transaction.");
        if (await db.Workshops.AnyAsync(cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        static DateTime At(int hour, int minute = 0) => new(2026, 10, 15, hour, minute, 0, DateTimeKind.Utc);
        db.Workshops.AddRange(
            new Workshop { Id = 1, Title = "Designing accessible interfaces", Start = At(8), End = At(9, 30), Capacity = 4 },
            new Workshop { Id = 2, Title = "Practical AI prototyping", Start = At(8, 45), End = At(10), Capacity = 2 },
            new Workshop { Id = 3, Title = "Design systems that scale", Start = At(9, 30), End = At(11), Capacity = 3 },
            new Workshop { Id = 4, Title = "Shipping resilient APIs", Start = At(11), End = At(12, 30), Capacity = 6 });
        db.Participants.AddRange(
            new Participant { Id = 1, Name = "Maya Chen", Email = "maya.chen@example.com" },
            new Participant { Id = 2, Name = "Oliver Berg", Email = "oliver.berg@example.com" },
            new Participant { Id = 3, Name = "Amara Okafor", Email = "amara.okafor@example.com" },
            new Participant { Id = 4, Name = "Lucas Andersen", Email = "lucas.andersen@example.com" },
            new Participant { Id = 5, Name = "Sofia Martinez", Email = "sofia.martinez@example.com" },
            new Participant { Id = 6, Name = "Noah Williams", Email = "noah.williams@example.com" });
        await db.SaveChangesAsync(cancellationToken);

        // Demo registrations must precede real requests regardless of when the
        // app is first launched; a fixed future timestamp would jump new arrivals
        // ahead of the existing waitlist.
        var created = DateTime.UtcNow.AddMinutes(-20);
        Registration Seed(long workshopId, long participantId, RegistrationStatus status, int sequence) => new()
        {
            WorkshopId = workshopId, ParticipantId = participantId, Status = status,
            Created = created.AddMinutes(sequence)
        };
        // Maya is first in workshop 2's queue but has an overlap. Sofia is the next eligible attendee.
        db.Registrations.AddRange(
            Seed(1, 1, RegistrationStatus.Confirmed, 0),
            Seed(1, 2, RegistrationStatus.Confirmed, 1),
            Seed(2, 3, RegistrationStatus.Confirmed, 2),
            Seed(2, 4, RegistrationStatus.Confirmed, 3),
            Seed(2, 1, RegistrationStatus.Waitlisted, 4),
            Seed(2, 5, RegistrationStatus.Waitlisted, 5),
            Seed(3, 2, RegistrationStatus.Confirmed, 6),
            Seed(4, 6, RegistrationStatus.Confirmed, 7),
            Seed(4, 5, RegistrationStatus.Cancelled, 8));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private const string WorkshopCreationSchema = """
        CREATE TABLE IF NOT EXISTS "WorkshopCreationRequests" (
            "Key" TEXT NOT NULL CONSTRAINT "PK_WorkshopCreationRequests" PRIMARY KEY,
            "Fingerprint" TEXT NOT NULL,
            "WorkshopId" INTEGER NOT NULL,
            "ResponseJson" TEXT NOT NULL,
            "Created" INTEGER NOT NULL,
            CONSTRAINT "FK_WorkshopCreationRequests_Workshops_WorkshopId"
                FOREIGN KEY ("WorkshopId") REFERENCES "Workshops" ("Id") ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS "IX_WorkshopCreationRequests_WorkshopId"
            ON "WorkshopCreationRequests" ("WorkshopId");
        """;

    private const string Triggers = """
        CREATE TRIGGER IF NOT EXISTS registration_capacity_insert
        BEFORE INSERT ON Registrations WHEN NEW.Status = 'Confirmed'
        BEGIN
            SELECT RAISE(ABORT, 'seatflow_capacity')
            WHERE (SELECT COUNT(*) FROM Registrations WHERE WorkshopId = NEW.WorkshopId AND Status = 'Confirmed')
                >= (SELECT Capacity FROM Workshops WHERE Id = NEW.WorkshopId);
        END;

        CREATE TRIGGER IF NOT EXISTS registration_capacity_update
        BEFORE UPDATE ON Registrations WHEN NEW.Status = 'Confirmed'
        BEGIN
            SELECT RAISE(ABORT, 'seatflow_capacity')
            WHERE (SELECT COUNT(*) FROM Registrations
                WHERE WorkshopId = NEW.WorkshopId AND Status = 'Confirmed' AND Id != NEW.Id)
                >= (SELECT Capacity FROM Workshops WHERE Id = NEW.WorkshopId);
        END;

        CREATE TRIGGER IF NOT EXISTS registration_overlap_insert
        BEFORE INSERT ON Registrations WHEN NEW.Status = 'Confirmed'
        BEGIN
            SELECT RAISE(ABORT, 'seatflow_overlap')
            WHERE EXISTS (
                SELECT 1 FROM Registrations r JOIN Workshops existing ON existing.Id = r.WorkshopId
                JOIN Workshops target ON target.Id = NEW.WorkshopId
                WHERE r.ParticipantId = NEW.ParticipantId AND r.Status = 'Confirmed'
                AND existing.Start < target.End AND target.Start < existing.End);
        END;

        CREATE TRIGGER IF NOT EXISTS registration_overlap_update
        BEFORE UPDATE ON Registrations WHEN NEW.Status = 'Confirmed'
        BEGIN
            SELECT RAISE(ABORT, 'seatflow_overlap')
            WHERE EXISTS (
                SELECT 1 FROM Registrations r JOIN Workshops existing ON existing.Id = r.WorkshopId
                JOIN Workshops target ON target.Id = NEW.WorkshopId
                WHERE r.ParticipantId = NEW.ParticipantId AND r.Status = 'Confirmed' AND r.Id != NEW.Id
                AND existing.Start < target.End AND target.Start < existing.End);
        END;

        CREATE TRIGGER IF NOT EXISTS workshop_capacity_update
        BEFORE UPDATE OF Capacity ON Workshops
        BEGIN
            SELECT RAISE(ABORT, 'seatflow_capacity')
            WHERE NEW.Capacity < (SELECT COUNT(*) FROM Registrations WHERE WorkshopId = NEW.Id AND Status = 'Confirmed');
        END;

        CREATE TRIGGER IF NOT EXISTS workshop_interval_update
        BEFORE UPDATE OF Start, End ON Workshops
        BEGIN
            SELECT RAISE(ABORT, 'seatflow_overlap')
            WHERE EXISTS (
                SELECT 1 FROM Registrations ours
                JOIN Registrations other ON other.ParticipantId = ours.ParticipantId AND other.WorkshopId != NEW.Id
                JOIN Workshops existing ON existing.Id = other.WorkshopId
                WHERE ours.WorkshopId = NEW.Id AND ours.Status = 'Confirmed' AND other.Status = 'Confirmed'
                AND existing.Start < NEW.End AND NEW.Start < existing.End);
        END;
        """;
}
