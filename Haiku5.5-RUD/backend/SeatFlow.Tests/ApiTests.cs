using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using SeatFlow.Api;
using SeatFlow.Api.Contracts;

namespace SeatFlow.Tests;

/// <summary>Hosts the real API against a temporary SQLite file (seeded on startup).</summary>
public sealed class SeatFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "seatflow-tests", $"api-{Guid.NewGuid():N}.db");

    public SeatFlowApiFactory()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:SeatFlow", $"Data Source={_dbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp database.
        }
    }
}

public class ApiTests(SeatFlowApiFactory factory) : IClassFixture<SeatFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Listing_workshops_shows_remaining_capacity()
    {
        var workshops = await _client.GetFromJsonAsync<List<WorkshopDto>>("/api/workshops");

        Assert.NotNull(workshops);
        Assert.True(workshops.Count >= 4);
        Assert.All(workshops, w => Assert.Equal(Math.Max(0, w.Capacity - w.ConfirmedCount), w.RemainingCapacity));
    }

    [Fact]
    public async Task Creating_a_workshop_with_invalid_data_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/workshops", new CreateWorkshopRequest(
            "", new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero), 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("title", body);
        Assert.Contains("endsAt", body);
        Assert.Contains("capacity", body);
    }

    [Fact]
    public async Task Registering_with_an_idempotency_key_replays_the_same_registration()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 3, day: 20);
        var participantId = await FirstParticipantIdAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new RegisterRequest(participantId))
        };
        request.Headers.Add("Idempotency-Key", "api-key-1");
        var replay = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new RegisterRequest(participantId))
        };
        replay.Headers.Add("Idempotency-Key", "api-key-1");

        var first = await _client.SendAsync(request);
        var second = await _client.SendAsync(replay);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<RegistrationDto>();
        var b = await second.Content.ReadFromJsonAsync<RegistrationDto>();
        Assert.Equal(a!.Id, b!.Id);
        Assert.Equal("Confirmed", a.Status);
    }

    [Fact]
    public async Task Registering_into_a_full_workshop_returns_a_waitlisted_registration()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 1, day: 21);
        var participants = await AllParticipantIdsAsync();

        var first = await RegisterAsync(workshopId, participants[0]);
        var second = await RegisterAsync(workshopId, participants[1]);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("Confirmed", (await first.Content.ReadFromJsonAsync<RegistrationDto>())!.Status);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("Waitlisted", (await second.Content.ReadFromJsonAsync<RegistrationDto>())!.Status);
    }

    [Fact]
    public async Task Duplicate_registration_returns_conflict()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 3, day: 22);
        var participantId = (await AllParticipantIdsAsync())[0];
        await RegisterAsync(workshopId, participantId);

        var duplicate = await RegisterAsync(workshopId, participantId);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Cancelling_twice_returns_ok_both_times_with_a_cancelled_registration()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 3, day: 23);
        var participantId = (await AllParticipantIdsAsync())[0];
        var registration = await (await RegisterAsync(workshopId, participantId)).Content.ReadFromJsonAsync<RegistrationDto>();

        var first = await _client.PostAsync($"/api/registrations/{registration!.Id}/cancel", null);
        var second = await _client.PostAsync($"/api/registrations/{registration.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<CancelRegistrationResponse>();
        Assert.Equal("Cancelled", secondBody!.Registration.Status);
        Assert.Null(secondBody.Promoted);
    }

    [Fact]
    public async Task Cancelling_an_unknown_registration_returns_not_found()
    {
        var response = await _client.PostAsync("/api/registrations/999999/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Registering_for_an_unknown_workshop_returns_not_found()
    {
        var participantId = (await AllParticipantIdsAsync())[0];

        var response = await RegisterAsync(999999, participantId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Empty_idempotency_key_is_rejected_with_validation_problem()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 3, day: 24);
        var participantId = (await AllParticipantIdsAsync())[0];
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new RegisterRequest(participantId))
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "   ");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_lists_a_participants_registrations_with_their_states()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 3, day: 25);
        var participantId = (await AllParticipantIdsAsync())[5];
        await RegisterAsync(workshopId, participantId);

        var schedule = await _client.GetFromJsonAsync<List<RegistrationDto>>($"/api/participants/{participantId}/schedule");

        Assert.NotNull(schedule);
        Assert.Contains(schedule, r => r.WorkshopId == workshopId && r.Status == "Confirmed");
    }

    [Fact]
    public async Task Schedule_for_an_unknown_participant_returns_not_found()
    {
        var response = await _client.GetAsync("/api/participants/999999/schedule");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> CreateWorkshopAsync(int capacity, int day)
    {
        var start = new DateTimeOffset(2027, 1, day, 9, 0, 0, TimeSpan.Zero);
        var response = await _client.PostAsJsonAsync("/api/workshops",
            new CreateWorkshopRequest($"API test {day}", start, start.AddHours(2), capacity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkshopDto>())!.Id;
    }

    private async Task<List<int>> AllParticipantIdsAsync()
    {
        var participants = await _client.GetFromJsonAsync<List<ParticipantDto>>("/api/participants");
        return participants!.Select(p => p.Id).ToList();
    }

    private async Task<int> FirstParticipantIdAsync() => (await AllParticipantIdsAsync())[0];

    private Task<HttpResponseMessage> RegisterAsync(int workshopId, int participantId) =>
        _client.PostAsJsonAsync($"/api/workshops/{workshopId}/registrations", new RegisterRequest(participantId));
}
