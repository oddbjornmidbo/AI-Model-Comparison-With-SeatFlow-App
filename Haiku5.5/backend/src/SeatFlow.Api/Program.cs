using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Core;

const int MaxIdempotencyKeyLength = 255;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SeatFlow") ?? "Data Source=seatflow.db";
builder.Services.AddDbContextFactory<SeatFlowDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<INotificationService, InMemoryNotificationService>();
builder.Services.AddSingleton<SeatFlowService>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Translate domain exceptions into HTTP responses so endpoints stay thin.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (SeatFlowException ex)
    {
        IResult result = ex switch
        {
            NotFoundException => Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not found"),
            ConflictException => Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict"),
            ValidationFailedException validation => Results.ValidationProblem(validation.Errors),
            _ => Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest)
        };
        await result.ExecuteAsync(context);
    }
});

var api = app.MapGroup("/api");

api.MapGet("/workshops", async (SeatFlowService service, CancellationToken ct) =>
{
    var summaries = await service.GetWorkshopsAsync(ct);
    return Results.Ok(summaries.Select(s => new WorkshopDto(
        s.Workshop.Id, s.Workshop.Title, Utc(s.Workshop.StartTime), Utc(s.Workshop.EndTime), s.Workshop.Capacity,
        s.ConfirmedCount, s.WaitlistedCount, s.RemainingCapacity)));
});

api.MapPost("/workshops", async (CreateWorkshopRequest request, SeatFlowService service, CancellationToken ct) =>
{
    var workshop = await service.CreateWorkshopAsync(
        request.Title ?? "", request.StartTime.UtcDateTime, request.EndTime.UtcDateTime, request.Capacity, ct);
    var dto = new WorkshopDto(workshop.Id, workshop.Title, Utc(workshop.StartTime), Utc(workshop.EndTime),
        workshop.Capacity, 0, 0, workshop.Capacity);
    return Results.Created($"/api/workshops/{workshop.Id}", dto);
});

api.MapGet("/participants", async (SeatFlowService service, CancellationToken ct) =>
{
    var participants = await service.GetParticipantsAsync(ct);
    return Results.Ok(participants.Select(p => new ParticipantDto(p.Id, p.Name, p.Email)));
});

api.MapGet("/workshops/{id:int}/registrations", async (int id, SeatFlowService service, CancellationToken ct) =>
{
    var registrations = await service.GetWorkshopRegistrationsAsync(id, ct);
    return Results.Ok(registrations.Select(ToDto));
});

api.MapPost("/workshops/{id:int}/registrations", async (
    int id,
    RegisterRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    SeatFlowService service,
    CancellationToken ct) =>
{
    if (idempotencyKey?.Length > MaxIdempotencyKeyLength)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Idempotency-Key"] = [$"Idempotency-Key must be {MaxIdempotencyKeyLength} characters or fewer."]
        });

    var result = await service.RegisterAsync(id, request.ParticipantId, idempotencyKey, ct);
    var dto = ToDto(result.Registration);

    // 201 for a new registration, 200 when an Idempotency-Key replay returns the original one.
    return result.Created
        ? Results.Created($"/api/workshops/{id}/registrations", dto)
        : Results.Ok(dto);
});

api.MapPost("/registrations/{id:int}/cancel", async (int id, SeatFlowService service, CancellationToken ct) =>
{
    var registration = await service.CancelAsync(id, ct);
    return Results.Ok(ToDto(registration));
});

api.MapGet("/participants/{id:int}/schedule", async (int id, SeatFlowService service, CancellationToken ct) =>
{
    var registrations = await service.GetParticipantScheduleAsync(id, ct);
    return Results.Ok(registrations.Select(ToDto));
});

await SeedData.EnsureSeededAsync(
    app.Services.GetRequiredService<IDbContextFactory<SeatFlowDbContext>>(),
    app.Services.GetRequiredService<SeatFlowService>());

app.Run();

static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

static RegistrationDto ToDto(Registration r) => new(
    r.Id, r.WorkshopId, r.Workshop.Title, Utc(r.Workshop.StartTime), Utc(r.Workshop.EndTime),
    r.ParticipantId, r.Participant.Name, r.Participant.Email, r.Status, Utc(r.CreatedAt));

public partial class Program;

public sealed record CreateWorkshopRequest(string? Title, DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity);

public sealed record RegisterRequest(int ParticipantId);

public sealed record WorkshopDto(
    int Id, string Title, DateTime StartTime, DateTime EndTime, int Capacity,
    int ConfirmedCount, int WaitlistedCount, int RemainingCapacity);

public sealed record ParticipantDto(int Id, string Name, string Email);

public sealed record RegistrationDto(
    int Id, int WorkshopId, string WorkshopTitle, DateTime WorkshopStartTime, DateTime WorkshopEndTime,
    int ParticipantId, string ParticipantName, string ParticipantEmail,
    RegistrationStatus Status, DateTime CreatedAt);
