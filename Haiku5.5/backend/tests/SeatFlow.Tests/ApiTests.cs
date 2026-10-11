using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace SeatFlow.Tests;

public sealed class ApiTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"seatflow-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:SeatFlow", $"Data Source={_path}"));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public async Task Creating_a_workshop_with_end_before_start_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/workshops", new
        {
            title = "Backwards",
            startTime = "2026-11-10T12:00:00Z",
            endTime = "2026-11-10T09:00:00Z",
            capacity = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registration_with_the_same_idempotency_key_returns_201_then_200_with_the_same_registration()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 5);
        var participantId = (await GetParticipantIdsAsync())[0];
        using var first = await PostRegistrationAsync(workshopId, participantId, "idem-1");

        using var replay = await PostRegistrationAsync(workshopId, participantId, "idem-1");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<RegistrationBody>();
        var replayBody = await replay.Content.ReadFromJsonAsync<RegistrationBody>();
        Assert.Equal(firstBody!.Id, replayBody!.Id);
        Assert.Equal("Confirmed", firstBody.Status);
    }

    [Fact]
    public async Task Registering_for_a_full_workshop_returns_a_waitlisted_registration()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 1);
        var participants = await GetParticipantIdsAsync();

        await PostRegistrationAsync(workshopId, participants[0], null);
        using var response = await PostRegistrationAsync(workshopId, participants[1], null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Waitlisted", (await response.Content.ReadFromJsonAsync<RegistrationBody>())!.Status);
    }

    [Fact]
    public async Task Registering_the_same_participant_twice_returns_409()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 5);
        var participantId = (await GetParticipantIdsAsync())[0];
        await PostRegistrationAsync(workshopId, participantId, null);

        using var response = await PostRegistrationAsync(workshopId, participantId, null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_twice_returns_200_both_times()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 5);
        var participantId = (await GetParticipantIdsAsync())[0];
        using var created = await PostRegistrationAsync(workshopId, participantId, null);
        var registrationId = (await created.Content.ReadFromJsonAsync<RegistrationBody>())!.Id;

        using var first = await _client.PostAsync($"/api/registrations/{registrationId}/cancel", null);
        using var second = await _client.PostAsync($"/api/registrations/{registrationId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Cancelled", (await second.Content.ReadFromJsonAsync<RegistrationBody>())!.Status);
    }

    [Fact]
    public async Task Cancelling_an_unknown_registration_returns_404()
    {
        using var response = await _client.PostAsync("/api/registrations/999999/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_schedule_returns_active_registrations_with_workshop_and_participant_details()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 5);
        var participantId = (await GetParticipantIdsAsync())[0];
        await PostRegistrationAsync(workshopId, participantId, null);

        var response = await _client.GetAsync($"/api/participants/{participantId}/schedule");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedule = await response.Content.ReadFromJsonAsync<List<ScheduleBody>>();
        var entry = schedule!.Single(e => e.WorkshopId == workshopId);
        Assert.Equal("Confirmed", entry.Status);
        Assert.False(string.IsNullOrEmpty(entry.ParticipantEmail));
    }

    [Fact]
    public async Task Workshop_registrations_list_returns_200_with_workshop_and_participant_details()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 5);
        var participantId = (await GetParticipantIdsAsync())[0];
        await PostRegistrationAsync(workshopId, participantId, null);

        var response = await _client.GetAsync($"/api/workshops/{workshopId}/registrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<ScheduleBody>>();
        var entry = Assert.Single(rows!);
        Assert.Equal(workshopId, entry.WorkshopId);
        Assert.Equal("Confirmed", entry.Status);
    }

    [Fact]
    public async Task Overlong_idempotency_key_is_rejected_with_400()
    {
        var workshopId = await CreateWorkshopAsync(capacity: 5);
        var participantId = (await GetParticipantIdsAsync())[0];

        using var response = await PostRegistrationAsync(workshopId, participantId, new string('k', 300));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<int> CreateWorkshopAsync(int capacity)
    {
        var response = await _client.PostAsJsonAsync("/api/workshops", new
        {
            title = $"Workshop {Guid.NewGuid():N}",
            startTime = "2026-12-01T09:00:00Z",
            endTime = "2026-12-01T10:00:00Z",
            capacity
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkshopBody>())!.Id;
    }

    private async Task<int[]> GetParticipantIdsAsync()
    {
        var participants = await _client.GetFromJsonAsync<ParticipantBody[]>("/api/participants");
        return participants!.Select(p => p.Id).ToArray();
    }

    private Task<HttpResponseMessage> PostRegistrationAsync(int workshopId, int participantId, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new { participantId })
        };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return _client.SendAsync(request);
    }

    private sealed record WorkshopBody(int Id);

    private sealed record ParticipantBody(int Id);

    private sealed record RegistrationBody(int Id, string Status);

    private sealed record ScheduleBody(int Id, int WorkshopId, string Status, string ParticipantEmail);
}
