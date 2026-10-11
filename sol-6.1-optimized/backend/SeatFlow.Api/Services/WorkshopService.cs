using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Services;

public sealed class WorkshopService(SeatFlowDbContext db, TimeProvider clock)
{
    public async Task<WorkshopCreationResult> CreateAsync(CreateWorkshopRequest request,
        string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (idempotencyKey is not null && (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 200 || idempotencyKey.Any(char.IsControl)))
            throw new DomainException(400, "invalid_idempotency_key", "Supply an Idempotency-Key header between 1 and 200 characters.");

        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 200)
            throw new DomainException(400, "invalid_title", "Enter a workshop title between 1 and 200 characters.");
        if (request.Capacity is < 1 or > 10000)
            throw new DomainException(400, "invalid_capacity", "Capacity must be between 1 and 10,000 participants.");
        if (request.Start == default || request.End == default || request.End <= request.Start)
            throw new DomainException(400, "invalid_interval", "The workshop end must be later than its start. Include a timezone in each date.");

        var start = request.Start.UtcDateTime;
        var end = request.End.UtcDateTime;
        var normalized = JsonSerializer.Serialize(new { title, start = start.Ticks, end = end.Ticks, request.Capacity }, ApiJson.Options);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            // Reserve the writer before the key lookup so separate connections
            // cannot both create a workshop for the same logical attempt.
            await using var sqliteTransaction = connection.BeginTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction, ct)
                ?? throw new InvalidOperationException("Unable to enlist the SQLite transaction.");

            if (idempotencyKey is not null)
            {
                var previous = await db.WorkshopCreationRequests.AsNoTracking()
                    .SingleOrDefaultAsync(r => r.Key == idempotencyKey, ct);
                if (previous is not null)
                {
                    if (previous.Fingerprint != fingerprint)
                        throw new DomainException(409, "idempotency_key_conflict", "This idempotency key was already used for a different workshop creation request.");
                    var original = JsonSerializer.Deserialize<WorkshopDto>(previous.ResponseJson, ApiJson.Options)
                        ?? throw new InvalidOperationException("The persisted workshop response is invalid.");
                    await transaction.CommitAsync(ct);
                    return new WorkshopCreationResult(original, true);
                }
            }

            var workshop = new Workshop { Title = title, Start = start, End = end, Capacity = request.Capacity };
            db.Workshops.Add(workshop);
            await db.SaveChangesAsync(ct);
            var dto = new WorkshopDto(workshop.Id, workshop.Title, workshop.Start, workshop.End,
                workshop.Capacity, 0, 0, workshop.Capacity);
            if (idempotencyKey is not null)
            {
                db.WorkshopCreationRequests.Add(new WorkshopCreationRequest
                {
                    Key = idempotencyKey, Fingerprint = fingerprint, WorkshopId = workshop.Id,
                    ResponseJson = JsonSerializer.Serialize(dto, ApiJson.Options), Created = clock.GetUtcNow().UtcDateTime
                });
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return new WorkshopCreationResult(dto, false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: 5 or 6 })
        {
            db.ChangeTracker.Clear();
            throw Busy();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
        {
            db.ChangeTracker.Clear();
            throw Busy();
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static DomainException Busy() => new(503, "database_busy",
        "The conference is receiving many requests. Retry this action with the same idempotency key.");
}
