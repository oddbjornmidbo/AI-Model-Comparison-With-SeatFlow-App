using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;

namespace SeatFlow.Tests;

public sealed class HttpIntegrationTests
{
    [Fact]
    public async Task Workshop_creation_returns_a_resolvable_location_and_normalized_UTC_times()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();
        var request = new CreateWorkshopRequest("Collaborative prototyping",
            new DateTimeOffset(2030, 6, 12, 12, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2030, 6, 12, 13, 0, 0, TimeSpan.FromHours(2)), 12);

        using var response = await client.PostAsJsonAsync("/api/workshops", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<WorkshopDto>(response);
        Assert.Equal(request.Start.UtcDateTime, created.Start);
        Assert.Equal(DateTimeKind.Utc, created.Start.Kind);
        Assert.Equal(12, created.RemainingSeats);
        Assert.NotNull(response.Headers.Location);
        using var resource = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, resource.StatusCode);
        Assert.Equal(created, await ReadAsync<WorkshopDto>(resource));
    }

    [Fact]
    public async Task Workshop_creation_supports_HTTP_replay_and_payload_binding_without_duplicate_rows()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();
        var start = new DateTimeOffset(2030, 6, 12, 23, 0, 0, TimeSpan.FromHours(2));
        var input = new CreateWorkshopRequest("Late workshop", start, start.AddHours(2), 12);

        using var first = await PostWorkshopAsync(client, input, "http-creation-attempt");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var workshop = await ReadAsync<WorkshopDto>(first);
        Assert.NotNull(first.Headers.Location);
        using var resource = await client.GetAsync(first.Headers.Location);
        Assert.Equal(workshop, await ReadAsync<WorkshopDto>(resource));

        using var replay = await PostWorkshopAsync(client, input, "http-creation-attempt");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        Assert.Equal(workshop, await ReadAsync<WorkshopDto>(replay));
        using var conflict = await PostWorkshopAsync(client, input with { Capacity = 5 }, "http-creation-attempt");
        await AssertProblemAsync(conflict, HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.Equal("idempotency_key_conflict", document.RootElement.GetProperty("code").GetString());

        await using var fresh = db.NewContext();
        Assert.Single(await fresh.Workshops.ToArrayAsync());
        Assert.Single(await fresh.WorkshopCreationRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Workshop_creation_rejects_multiple_or_overlong_keys_without_persisting_workshops()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();
        var start = new DateTimeOffset(2030, 6, 12, 10, 0, 0, TimeSpan.Zero);
        var input = new CreateWorkshopRequest("Valid workshop", start, start.AddHours(1), 2);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workshops") { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", new[] { "first-key", "second-key" });

        using var multiple = await client.SendAsync(request);
        await AssertProblemAsync(multiple, HttpStatusCode.BadRequest);
        using var overlong = await PostWorkshopAsync(client, input, new string('k', 201));
        await AssertProblemAsync(overlong, HttpStatusCode.BadRequest);
        await using var fresh = db.NewContext();
        Assert.Empty(await fresh.Workshops.ToArrayAsync());
        Assert.Empty(await fresh.WorkshopCreationRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Registration_supports_HTTP_replay_binding_conflict_and_resolvable_location()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var workshop = await db.WorkshopAsync(capacity: 2);
        var participant = await db.ParticipantAsync();
        var otherParticipant = await db.ParticipantAsync("Someone else");
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();

        using var first = await PostRegistrationAsync(client, workshop.Id, participant.Id, "http-attempt");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var registration = await ReadAsync<RegistrationDto>(first);
        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        Assert.NotNull(first.Headers.Location);
        using var resource = await client.GetAsync(first.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, resource.StatusCode);
        Assert.Equal(registration.Id, (await ReadAsync<RegistrationDto>(resource)).Id);

        using var replay = await PostRegistrationAsync(client, workshop.Id, participant.Id, "http-attempt");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(registration, await ReadAsync<RegistrationDto>(replay));
        using var conflict = await PostRegistrationAsync(client, workshop.Id, otherParticipant.Id, "http-attempt");
        await AssertProblemAsync(conflict, HttpStatusCode.Conflict);
        Assert.Single(await db.RegistrationsAsync());
    }

    [Fact]
    public async Task Cancellation_HTTP_response_and_all_derived_views_agree_after_promotion()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var workshop = await db.WorkshopAsync();
        var owner = await db.ParticipantAsync("Booked attendee");
        var queuedParticipant = await db.ParticipantAsync("Waiting attendee");
        var confirmed = await db.RegisterAsync(workshop, owner);
        var queued = await db.RegisterAsync(workshop, queuedParticipant);
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();

        using var cancellation = await client.PostAsync($"/api/registrations/{confirmed.Registration.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);
        var result = await ReadAsync<CancellationResult>(cancellation);
        Assert.Equal(RegistrationStatus.Cancelled, result.Registration.Status);
        Assert.Equal(queued.Registration.Id, result.PromotedRegistration?.Id);

        var roster = await client.GetFromJsonAsync<RegistrationDto[]>($"/api/workshops/{workshop.Id}/registrations", ApiJson.Options);
        Assert.NotNull(roster);
        Assert.Equal(RegistrationStatus.Cancelled, roster.Single(r => r.ParticipantId == owner.Id).Status);
        Assert.Equal(RegistrationStatus.Confirmed, roster.Single(r => r.ParticipantId == queuedParticipant.Id).Status);
        var workshops = await client.GetFromJsonAsync<WorkshopDto[]>("/api/workshops", ApiJson.Options);
        var current = Assert.Single(workshops!);
        Assert.Equal(1, current.ConfirmedCount);
        Assert.Equal(0, current.WaitlistedCount);
        Assert.Equal(0, current.RemainingSeats);
        Assert.Empty((await client.GetFromJsonAsync<RegistrationDto[]>($"/api/participants/{owner.Id}/schedule", ApiJson.Options))!);
        Assert.Equal(queued.Registration.Id,
            Assert.Single((await client.GetFromJsonAsync<RegistrationDto[]>($"/api/participants/{queuedParticipant.Id}/schedule", ApiJson.Options))!).Id);

        using var repeated = await client.PostAsync($"/api/registrations/{confirmed.Registration.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Null((await ReadAsync<CancellationResult>(repeated)).PromotedRegistration);
    }

    [Fact]
    public async Task Missing_idempotency_key_and_invalid_registration_requests_return_ProblemDetails()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var workshop = await db.WorkshopAsync();
        var participant = await db.ParticipantAsync();
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();

        using var missingKey = await client.PostAsJsonAsync($"/api/workshops/{workshop.Id}/registrations", new { participantId = participant.Id });
        await AssertProblemAsync(missingKey, HttpStatusCode.BadRequest);
        using var invalidParticipant = await PostRegistrationAsync(client, workshop.Id, 0, "invalid-participant");
        await AssertProblemAsync(invalidParticipant, HttpStatusCode.BadRequest);
        using var malformed = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshop.Id}/registrations")
        {
            Content = new StringContent("{\"participantId\":", System.Text.Encoding.UTF8, "application/json")
        };
        malformed.Headers.Add("Idempotency-Key", "malformed-request");
        using var malformedResponse = await client.SendAsync(malformed);
        await AssertProblemAsync(malformedResponse, HttpStatusCode.BadRequest);
        Assert.Empty(await db.RegistrationsAsync());
    }

    [Theory]
    [InlineData("", 2, 1)]
    [InlineData("Valid title", 0, 1)]
    [InlineData("Valid title", 2, 0)]
    [InlineData("Valid title", 2, -1)]
    public async Task Invalid_workshop_input_is_rejected_without_persisting_a_workshop(string title, int capacity, int durationHours)
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();
        var start = new DateTimeOffset(2030, 6, 12, 10, 0, 0, TimeSpan.Zero);

        using var response = await client.PostAsJsonAsync("/api/workshops", new CreateWorkshopRequest(title, start, start.AddHours(durationHours), capacity));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await client.GetFromJsonAsync<WorkshopDto[]>("/api/workshops", ApiJson.Options))!);
    }

    [Fact]
    public async Task Missing_resources_return_404_ProblemDetails()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var workshop = await db.WorkshopAsync();
        var participant = await db.ParticipantAsync();
        await using var app = new ApiFactory(db.ConnectionString);
        using var client = app.CreateClient();
        foreach (var path in new[] { "/api/workshops/99999", "/api/workshops/99999/registrations", "/api/registrations/99999", "/api/participants/99999/schedule" })
        {
            using var response = await client.GetAsync(path);
            await AssertProblemAsync(response, HttpStatusCode.NotFound);
        }
        using var missingWorkshop = await PostRegistrationAsync(client, 99999, participant.Id, "missing-workshop");
        await AssertProblemAsync(missingWorkshop, HttpStatusCode.NotFound);
        using var missingParticipant = await PostRegistrationAsync(client, workshop.Id, 99999, "missing-participant");
        await AssertProblemAsync(missingParticipant, HttpStatusCode.NotFound);
        using var missingCancellation = await client.PostAsync("/api/registrations/99999/cancel", null);
        await AssertProblemAsync(missingCancellation, HttpStatusCode.NotFound);
        Assert.Empty(await db.RegistrationsAsync());
    }

    private static async Task<HttpResponseMessage> PostRegistrationAsync(HttpClient client, long workshopId, long participantId, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new CreateRegistrationRequest(participantId))
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostWorkshopAsync(HttpClient client, CreateWorkshopRequest input, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workshops") { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options)) ?? throw new InvalidOperationException("Expected a JSON response.");

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("title").GetString()));
    }
}

internal sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SeatFlow"] = connectionString,
            ["SeatFlow:SeedData"] = "false"
        }));
        // The top-level application reads its connection string before the test
        // host's configuration callback. Replace the actual scoped registration
        // as well, ensuring HTTP requests use this test's file and fresh contexts.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SeatFlowDbContext>();
            services.RemoveAll<DbContextOptions<SeatFlowDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SeatFlowDbContext>>();
            services.AddDbContext<SeatFlowDbContext>(options => options.UseSqlite(connectionString));
        });
    }
}
