using Microsoft.EntityFrameworkCore;
using SeatFlow.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContextFactory<SeatFlowDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SeatFlow") ?? "Data Source=seatflow.db;Default Timeout=30"));
builder.Services.AddSingleton<MemoryNotificationService>();
builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<MemoryNotificationService>());
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (SeatFlowException ex)
    {
        context.Response.StatusCode = ex.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SeatFlowDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Workshops.AnyAsync())
    {
        var today = DateTimeOffset.UtcNow.Date.AddDays(1);
        db.Workshops.AddRange(
            new Workshop { Title = "Designing for Everyone", StartsAt = today.AddHours(9), EndsAt = today.AddHours(10.5), Capacity = 3 },
            new Workshop { Title = "Practical AI Prototyping", StartsAt = today.AddHours(10), EndsAt = today.AddHours(11.5), Capacity = 2 },
            new Workshop { Title = "Building Better Teams", StartsAt = today.AddHours(13), EndsAt = today.AddHours(14.5), Capacity = 4 },
            new Workshop { Title = "Accessible Web Patterns", StartsAt = today.AddHours(14), EndsAt = today.AddHours(15), Capacity = 3 });
        db.Participants.AddRange(
            new Participant { Name = "Alex Morgan", Email = "alex@example.test" },
            new Participant { Name = "Sam Lee", Email = "sam@example.test" },
            new Participant { Name = "Taylor Kim", Email = "taylor@example.test" },
            new Participant { Name = "Jordan Patel", Email = "jordan@example.test" },
            new Participant { Name = "Casey Rivera", Email = "casey@example.test" },
            new Participant { Name = "Morgan Chen", Email = "morgan@example.test" });
        await db.SaveChangesAsync();
    }
}

app.MapGet("/api/workshops", async (IDbContextFactory<SeatFlowDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    return await db.Workshops.OrderBy(x => x.StartsAt).Select(x => new WorkshopView(x.Id, x.Title, x.StartsAt, x.EndsAt, x.Capacity,
        x.Capacity - x.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed))).ToListAsync();
});

app.MapGet("/api/participants", async (IDbContextFactory<SeatFlowDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    return await db.Participants.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Email }).ToListAsync();
});

app.MapPost("/api/workshops", async (CreateWorkshopRequest request, IDbContextFactory<SeatFlowDbContext> factory) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 120 || request.Capacity < 1 || request.EndsAt <= request.StartsAt)
        return Results.BadRequest(new { error = "Provide a title, positive capacity, and an end time after the start time." });
    await using var db = await factory.CreateDbContextAsync();
    var workshop = new Workshop { Title = request.Title.Trim(), StartsAt = request.StartsAt, EndsAt = request.EndsAt, Capacity = request.Capacity };
    db.Workshops.Add(workshop);
    await db.SaveChangesAsync();
    return Results.Created($"/api/workshops/{workshop.Id}", new WorkshopView(workshop.Id, workshop.Title, workshop.StartsAt, workshop.EndsAt, workshop.Capacity, workshop.Capacity));
});

app.MapGet("/api/workshops/{id:int}/registrations", async (int id, IDbContextFactory<SeatFlowDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    if (!await db.Workshops.AnyAsync(x => x.Id == id)) return Results.NotFound(new { error = "Workshop not found." });
    var regs = await db.Registrations.Where(x => x.WorkshopId == id).Include(x => x.Participant).OrderBy(x => x.Status).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
        .Select(x => new RegistrationView(x.Id, x.WorkshopId, x.ParticipantId, x.Participant!.Name, x.Participant.Email, x.Status.ToString(), x.CreatedAt)).ToListAsync();
    return Results.Ok(regs);
});

app.MapPost("/api/workshops/{id:int}/registrations", async (int id, CreateRegistrationRequest request, HttpRequest http, RegistrationService service, CancellationToken ct) =>
{
    if (request.ParticipantId < 1) return Results.BadRequest(new { error = "ParticipantId must be positive." });
    var (registration, created) = await service.RegisterAsync(id, request.ParticipantId, http.Headers["Idempotency-Key"].FirstOrDefault(), ct);
    return Results.Json(new RegistrationResult(registration.Id, registration.WorkshopId, registration.ParticipantId, registration.Status.ToString(), registration.CreatedAt), statusCode: created ? StatusCodes.Status201Created : StatusCodes.Status200OK);
});

app.MapPost("/api/registrations/{id:int}/cancel", async (int id, RegistrationService service, CancellationToken ct) =>
{
    var registration = await service.CancelAsync(id, ct);
    return Results.Ok(new RegistrationResult(registration.Id, registration.WorkshopId, registration.ParticipantId, registration.Status.ToString(), registration.CreatedAt));
});

app.MapGet("/api/participants/{id:int}/schedule", async (int id, IDbContextFactory<SeatFlowDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var participant = await db.Participants.FindAsync(id);
    if (participant is null) return Results.NotFound(new { error = "Participant not found." });
    var schedule = await db.Registrations.Where(x => x.ParticipantId == id).Include(x => x.Workshop).OrderBy(x => x.Workshop!.StartsAt)
        .Select(x => new ScheduleItem(x.Id, x.Workshop!.Id, x.Workshop.Title, x.Workshop.StartsAt, x.Workshop.EndsAt, x.Status.ToString())).ToListAsync();
    return Results.Ok(new ParticipantSchedule(id, participant.Name, schedule));
});

app.Run();

public partial class Program;
public sealed record CreateWorkshopRequest(string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity);
public sealed record CreateRegistrationRequest(int ParticipantId);
public sealed record WorkshopView(int Id, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, int RemainingCapacity);
public sealed record RegistrationView(int Id, int WorkshopId, int ParticipantId, string ParticipantName, string ParticipantEmail, string Status, DateTimeOffset CreatedAt);
public sealed record RegistrationResult(int Id, int WorkshopId, int ParticipantId, string Status, DateTimeOffset CreatedAt);
public sealed record ScheduleItem(int RegistrationId, int WorkshopId, string WorkshopTitle, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status);
public sealed record ParticipantSchedule(int ParticipantId, string ParticipantName, IReadOnlyList<ScheduleItem> Registrations);
