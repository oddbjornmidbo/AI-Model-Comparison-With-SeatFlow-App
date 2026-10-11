using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SeatFlow")
    ?? "Data Source=seatflow.db;Foreign Keys=True;Default Timeout=30;Pooling=True";
builder.Services.AddDbContext<SeatFlowDbContext>(options => options.UseSqlite(connectionString));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<INotificationService, LoggingNotificationService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<WorkshopService>();
builder.Services.AddScoped<ReadService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://127.0.0.1:5174")
        .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Location", "Idempotency-Replayed")));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

await using (var scope = app.Services.CreateAsyncScope())
{
    var seed = builder.Configuration.GetValue("SeatFlow:SeedData", !app.Environment.IsEnvironment("Testing"));
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<SeatFlowDbContext>(), seed);
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", application = "SeatFlow" }));
app.MapGet("/api/workshops", (ReadService service, CancellationToken ct) => service.GetWorkshopsAsync(ct));
app.MapGet("/api/workshops/{id:long}", (long id, ReadService service, CancellationToken ct) => service.GetWorkshopAsync(id, ct));
app.MapGet("/api/participants", (ReadService service, CancellationToken ct) => service.GetParticipantsAsync(ct));
app.MapGet("/api/participants/{id:long}", (long id, ReadService service, CancellationToken ct) => service.GetParticipantAsync(id, ct));
app.MapGet("/api/workshops/{id:long}/registrations", (long id, ReadService service, CancellationToken ct) => service.GetWorkshopRegistrationsAsync(id, ct));
app.MapGet("/api/registrations/{id:long}", (long id, ReadService service, CancellationToken ct) => service.GetRegistrationAsync(id, ct));
app.MapGet("/api/participants/{id:long}/schedule", (long id, ReadService service, CancellationToken ct) => service.GetScheduleAsync(id, ct));

app.MapPost("/api/workshops", async (CreateWorkshopRequest request, HttpContext context,
    WorkshopService service, CancellationToken ct) =>
{
    var keys = context.Request.Headers["Idempotency-Key"];
    if (keys.Count > 1)
        throw new DomainException(400, "invalid_idempotency_key", "Supply at most one Idempotency-Key header.");
    var result = await service.CreateAsync(request, keys.Count == 0 ? null : keys[0] ?? "", ct);
    if (result.IsReplay)
    {
        context.Response.Headers["Idempotency-Replayed"] = "true";
        return Results.Ok(result.Workshop);
    }
    return Results.Created($"/api/workshops/{result.Workshop.Id}", result.Workshop);
});

app.MapPost("/api/workshops/{id:long}/registrations", async (long id, CreateRegistrationRequest request,
    HttpContext context, RegistrationService service, CancellationToken ct) =>
{
    var keys = context.Request.Headers["Idempotency-Key"];
    if (keys.Count != 1)
        throw new DomainException(400, "invalid_idempotency_key", "Supply exactly one Idempotency-Key header.");
    var result = await service.RegisterAsync(id, request.ParticipantId, keys[0] ?? "", ct);
    if (result.IsReplay)
    {
        context.Response.Headers["Idempotency-Replayed"] = "true";
        return Results.Ok(result.Registration);
    }
    return Results.Created($"/api/registrations/{result.Registration.Id}", result.Registration);
});

app.MapPost("/api/registrations/{id:long}/cancel", (long id, RegistrationService service, CancellationToken ct) => service.CancelAsync(id, ct));

app.Run();

public partial class Program;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, detail) = exception switch
        {
            DomainException domain => (domain.StatusCode, domain.Code, domain.Message),
            BadHttpRequestException => (400, "invalid_request", "Check the request body and provide valid JSON with all required fields."),
            _ => (500, "internal_error", "The request could not be completed. Please try again.")
        };
        if (status >= 500)
            logger.LogError(exception, "Request {Method} {Path} failed with {ErrorCode}", context.Request.Method, context.Request.Path, code);
        if (status == 503) context.Response.Headers.RetryAfter = "1";
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = status switch { 400 => "Invalid request", 404 => "Resource not found", 409 => "Request conflict", 503 => "Please retry", _ => "Unexpected error" },
            Detail = detail,
            Instance = context.Request.Path,
            Extensions = { ["code"] = code }
        }, options: ApiJson.Options, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
