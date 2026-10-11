using Microsoft.AspNetCore.Mvc;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Services;

namespace SeatFlow.Api;

public static class SeatFlowEndpoints
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public static void MapSeatFlowApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/workshops", async (CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListWorkshopsAsync(ct)));

        api.MapPost("/workshops", async (CreateWorkshopRequest request, CatalogService catalog, CancellationToken ct) =>
        {
            var errors = ValidateWorkshop(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var created = await catalog.CreateWorkshopAsync(request.Title!, request.StartsAt, request.EndsAt, request.Capacity, ct);
            return Results.Created($"/api/workshops/{created.Id}", created);
        });

        api.MapGet("/workshops/{id:int}/registrations", async (int id, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListWorkshopRegistrationsAsync(id, ct)));

        api.MapPost("/workshops/{id:int}/registrations", async (
            int id,
            RegisterRequest request,
            [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
            RegistrationService registrations,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.ParticipantId <= 0)
            {
                errors["participantId"] = ["participantId must be a positive integer."];
            }

            var key = idempotencyKey?.Trim();
            if (idempotencyKey is not null && (key!.Length == 0 || key.Length > 200))
            {
                errors[IdempotencyKeyHeader] = ["Idempotency-Key must be 1-200 characters."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await registrations.RegisterAsync(id, request.ParticipantId, key, ct);
            var dto = RegistrationDto.From(result.Registration);

            // A replayed request returns the original registration with 200; a new one is 201.
            return result.Replayed ? Results.Ok(dto) : Results.Created($"/api/registrations/{dto.Id}", dto);
        });

        api.MapPost("/registrations/{id:int}/cancel", async (int id, RegistrationService registrations, CancellationToken ct) =>
        {
            var result = await registrations.CancelAsync(id, ct);
            var response = new CancelRegistrationResponse(
                RegistrationDto.From(result.Registration),
                result.Promoted is null ? null : RegistrationDto.From(result.Promoted));
            return Results.Ok(response);
        });

        api.MapGet("/participants", async (CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListParticipantsAsync(ct)));

        api.MapGet("/participants/{id:int}/schedule", async (int id, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.GetScheduleAsync(id, ct)));
    }

    private static Dictionary<string, string[]> ValidateWorkshop(CreateWorkshopRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (request.Title.Trim().Length > 200)
        {
            errors["title"] = ["Title must be at most 200 characters."];
        }

        if (request.EndsAt <= request.StartsAt)
        {
            errors["endsAt"] = ["End time must be after start time."];
        }

        if (request.Capacity < 1 || request.Capacity > 500)
        {
            errors["capacity"] = ["Capacity must be between 1 and 500."];
        }

        return errors;
    }
}
