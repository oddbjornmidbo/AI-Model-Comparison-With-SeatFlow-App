using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api;

var builder = WebApplication.CreateBuilder(args);
var databasePath = builder.Configuration["DatabasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "seatflow.db");
var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    DefaultTimeout = 30,
    ForeignKeys = true
}.ToString();

builder.Services.AddDbContext<SeatFlowDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<DevelopmentNotificationService>();
builder.Services.AddSingleton<INotificationService>(services => services.GetRequiredService<DevelopmentNotificationService>());
builder.Services.AddScoped<RegistrationService>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (DomainException exception)
    {
        context.Response.StatusCode = exception.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    }
});

using (var scope = app.Services.CreateScope())
    await SeedData.InitializeAsync(scope.ServiceProvider.GetRequiredService<SeatFlowDbContext>());

app.MapGet("/api/workshops", async (RegistrationService service, CancellationToken ct) =>
    Results.Ok(await service.GetWorkshopsAsync(ct)));

app.MapPost("/api/workshops", async (WorkshopInput input, RegistrationService service, CancellationToken ct) =>
{
    var workshop = await service.CreateWorkshopAsync(input, ct);
    return Results.CreatedAtRoute("GetWorkshop", new { id = workshop.Id }, workshop);
}).WithName("CreateWorkshop");

app.MapGet("/api/workshops/{id:guid}", async (Guid id, RegistrationService service, CancellationToken ct) =>
    (await service.GetWorkshopAsync(id, ct)) is { } workshop ? Results.Ok(workshop) : Results.NotFound()).WithName("GetWorkshop");

app.MapGet("/api/workshops/{id:guid}/registrations", async (Guid id, RegistrationService service, CancellationToken ct) =>
    Results.Ok(await service.GetWorkshopRegistrationsAsync(id, ct)));

app.MapPost("/api/workshops/{id:guid}/registrations", async (Guid id, RegistrationInput input, HttpRequest request, RegistrationService service, CancellationToken ct) =>
{
    var key = request.Headers["Idempotency-Key"].ToString();
    var registration = await service.RegisterAsync(id, input.ParticipantId, key, ct);
    return Results.CreatedAtRoute("GetRegistration", new { id = registration.Id }, registration);
});

app.MapGet("/api/registrations/{id:guid}", async (Guid id, RegistrationService service, CancellationToken ct) =>
    (await service.GetRegistrationAsync(id, ct)) is { } registration ? Results.Ok(registration) : Results.NotFound()).WithName("GetRegistration");

app.MapPost("/api/registrations/{id:guid}/cancel", async (Guid id, RegistrationService service, CancellationToken ct) =>
    Results.Ok(await service.CancelAsync(id, ct)));

app.MapGet("/api/participants", async (SeatFlowDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Participants.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Email }).ToListAsync(ct)));

app.MapGet("/api/participants/{id:guid}/schedule", async (Guid id, RegistrationService service, CancellationToken ct) =>
    Results.Ok(await service.GetScheduleAsync(id, ct)));

app.Run();

public partial class Program;
