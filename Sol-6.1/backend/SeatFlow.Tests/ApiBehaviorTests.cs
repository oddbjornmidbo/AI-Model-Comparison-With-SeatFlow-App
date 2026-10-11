using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;
using Xunit;

namespace SeatFlow.Tests;

public sealed class ApiBehaviorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Registration_cancellation_and_promotion_update_lists_capacity_and_schedules()
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();
        var (workshopId, participants) = await app.SeedAsync(capacity: 1);

        using var confirmedResponse = await client.PostAsJsonAsync($"/api/workshops/{workshopId}/registrations",
            new { participantId = participants[0] });
        Assert.Equal(HttpStatusCode.Created, confirmedResponse.StatusCode);
        Assert.NotNull(confirmedResponse.Headers.Location);
        var confirmed = await ReadAsync<RegistrationDto>(confirmedResponse);
        Assert.Equal(RegistrationStatus.Confirmed, confirmed.Status);
        using var locationResponse = await client.GetAsync(confirmedResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, locationResponse.StatusCode);
        Assert.Equal(confirmed, await ReadAsync<RegistrationDto>(locationResponse));

        var waiting = await RegisterAsync(client, workshopId, participants[1]);
        var later = await RegisterAsync(client, workshopId, participants[2]);
        Assert.Equal(RegistrationStatus.Waitlisted, waiting.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, later.Status);
        var full = Assert.Single(await client.GetFromJsonAsync<WorkshopDto[]>("/api/workshops", JsonOptions) ?? []);
        Assert.Equal(0, full.RemainingCapacity);
        Assert.Equal(1, full.ConfirmedCount);
        Assert.Equal(2, full.WaitlistedCount);

        using var cancellationResponse = await client.PostAsync($"/api/registrations/{confirmed.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancellationResponse.StatusCode);
        Assert.Equal(RegistrationStatus.Cancelled, (await ReadAsync<RegistrationDto>(cancellationResponse)).Status);
        var registrations = await client.GetFromJsonAsync<RegistrationDto[]>(
            $"/api/workshops/{workshopId}/registrations", JsonOptions) ?? [];
        Assert.Equal(RegistrationStatus.Cancelled, Assert.Single(registrations, r => r.Id == confirmed.Id).Status);
        Assert.Equal(RegistrationStatus.Confirmed, Assert.Single(registrations, r => r.Id == waiting.Id).Status);
        Assert.Equal(RegistrationStatus.Waitlisted, Assert.Single(registrations, r => r.Id == later.Id).Status);
        var refreshed = Assert.Single(await client.GetFromJsonAsync<WorkshopDto[]>("/api/workshops", JsonOptions) ?? []);
        Assert.Equal(1, refreshed.ConfirmedCount);
        Assert.Equal(1, refreshed.WaitlistedCount);
        Assert.Equal(0, refreshed.RemainingCapacity);
        Assert.Empty(await client.GetFromJsonAsync<ScheduleDto[]>($"/api/participants/{participants[0]}/schedule", JsonOptions) ?? []);
        var promotedSchedule = Assert.Single(await client.GetFromJsonAsync<ScheduleDto[]>(
            $"/api/participants/{participants[1]}/schedule", JsonOptions) ?? []);
        Assert.Equal(workshopId, promotedSchedule.WorkshopId);
        Assert.Equal(waiting.Id, promotedSchedule.RegistrationId);
        Assert.Empty(await client.GetFromJsonAsync<ScheduleDto[]>($"/api/participants/{participants[2]}/schedule", JsonOptions) ?? []);
    }

    [Fact]
    public async Task Idempotency_header_replays_registration_and_rejects_different_payload()
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();
        var (workshopId, participants) = await app.SeedAsync();

        var first = await SendWithKeyAsync(client, workshopId, participants[0], "http-retry");
        var repeat = await SendWithKeyAsync(client, workshopId, participants[0], "http-retry");
        Assert.Equal(first, repeat);
        using var differentRequest = CreateRequest(workshopId, participants[1], "http-retry");
        using var conflictResponse = await client.SendAsync(differentRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync<ProblemDetails>(conflictResponse);
        Assert.Equal("IdempotencyKeyConflict", problem.Title);
        Assert.Single(await client.GetFromJsonAsync<RegistrationDto[]>(
            $"/api/workshops/{workshopId}/registrations", JsonOptions) ?? []);
    }

    [Fact]
    public async Task Duplicate_active_registration_returns_conflict_problem()
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();
        var (workshopId, participants) = await app.SeedAsync();
        await RegisterAsync(client, workshopId, participants[0]);

        using var response = await client.PostAsJsonAsync($"/api/workshops/{workshopId}/registrations",
            new { participantId = participants[0] });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DuplicateRegistration", (await ReadAsync<ProblemDetails>(response)).Title);
    }

    [Theory]
    [InlineData("/api/workshops/999999/registrations")]
    [InlineData("/api/participants/999999/schedule")]
    [InlineData("/api/registrations/999999")]
    public async Task Unknown_get_resource_returns_not_found_problem(string route)
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();

        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, (await ReadAsync<ProblemDetails>(response)).Status);
    }

    [Fact]
    public async Task Invalid_registration_payload_and_unknown_participant_have_distinct_statuses()
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();
        var (workshopId, _) = await app.SeedAsync();

        using var invalidResponse = await client.PostAsJsonAsync($"/api/workshops/{workshopId}/registrations",
            new { participantId = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var invalidProblem = await ReadAsync<ValidationProblemDetails>(invalidResponse);
        Assert.NotEmpty(invalidProblem.Errors);
        using var unknownResponse = await client.PostAsJsonAsync($"/api/workshops/{workshopId}/registrations",
            new { participantId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.Equal("ParticipantNotFound", (await ReadAsync<ProblemDetails>(unknownResponse)).Title);
    }

    [Fact]
    public async Task Creating_workshop_validates_capacity_title_and_time_range()
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();

        using var invalidResponse = await client.PostAsJsonAsync("/api/workshops", new
        {
            title = "   ", startTime = "2030-01-15T11:00:00Z", endTime = "2030-01-15T10:00:00Z", capacity = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.NotEmpty((await ReadAsync<ValidationProblemDetails>(invalidResponse)).Errors);
        Assert.Empty(await client.GetFromJsonAsync<WorkshopDto[]>("/api/workshops", JsonOptions) ?? []);
    }

    [Fact]
    public async Task Creating_workshop_trims_title_and_normalizes_times_to_utc()
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/workshops", new
        {
            title = "  Practical SQL  ", startTime = "2030-01-15T11:00:00+02:00", endTime = "2030-01-15T12:00:00+02:00", capacity = 3
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var workshop = await ReadAsync<WorkshopDto>(response);
        Assert.Equal("Practical SQL", workshop.Title);
        Assert.Equal(new DateTime(2030, 1, 15, 9, 0, 0, DateTimeKind.Utc), workshop.StartTime);
        Assert.Equal(3, workshop.RemainingCapacity);
        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(workshop, await ReadAsync<WorkshopDto>(getResponse));
    }

    [Fact]
    public async Task Cancelling_twice_over_http_returns_same_cancelled_registration()
    {
        await using var app = new TestApp();
        using var client = app.CreateClient();
        var (workshopId, participants) = await app.SeedAsync();
        var registration = await RegisterAsync(client, workshopId, participants[0]);

        using var firstResponse = await client.PostAsync($"/api/registrations/{registration.Id}/cancel", null);
        using var secondResponse = await client.PostAsync($"/api/registrations/{registration.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(await ReadAsync<RegistrationDto>(firstResponse), await ReadAsync<RegistrationDto>(secondResponse));
        var workshop = Assert.Single(await client.GetFromJsonAsync<WorkshopDto[]>("/api/workshops", JsonOptions) ?? []);
        Assert.Equal(workshop.Capacity, workshop.RemainingCapacity);
    }

    private static async Task<RegistrationDto> RegisterAsync(HttpClient client, int workshopId, int participantId)
    {
        using var response = await client.PostAsJsonAsync($"/api/workshops/{workshopId}/registrations", new { participantId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<RegistrationDto>(response);
    }

    private static async Task<RegistrationDto> SendWithKeyAsync(HttpClient client, int workshopId, int participantId, string key)
    {
        using var request = CreateRequest(workshopId, participantId, key);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<RegistrationDto>(response);
    }

    private static HttpRequestMessage CreateRequest(int workshopId, int participantId, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new { participantId })
        };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions)
        ?? throw new InvalidOperationException("Expected a JSON response.");
}

internal sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"seatflow-http-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SeatFlow.Api")));
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["SeedData"] = "false", ["Logging:LogLevel:Default"] = "Warning" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<SeatFlowDbContext>();
            services.RemoveAll<DbContextOptions<SeatFlowDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SeatFlowDbContext>>();
            services.AddDbContext<SeatFlowDbContext>(options =>
                options.UseSqlite($"Data Source={_path};Default Timeout=30;Pooling=False"));
        });
    }

    public async Task<(int WorkshopId, int[] ParticipantIds)> SeedAsync(int capacity = 2)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SeatFlowDbContext>();
        var workshop = new Workshop
        {
            Title = "HTTP workshop", Capacity = capacity,
            StartTime = new DateTime(2030, 1, 15, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2030, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };
        var participants = new[]
        {
            new Participant { Name = "Ada", Email = "ada@example.test" },
            new Participant { Name = "Ben", Email = "ben@example.test" },
            new Participant { Name = "Cleo", Email = "cleo@example.test" }
        };
        db.Workshops.Add(workshop);
        db.Participants.AddRange(participants);
        await db.SaveChangesAsync();
        return (workshop.Id, participants.Select(p => p.Id).ToArray());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_path);
        File.Delete(_path + "-wal");
        File.Delete(_path + "-shm");
    }
}
