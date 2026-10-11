using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace SeatFlow.Tests;

public sealed class ApiRouteTests
{
    [Fact]
    public async Task Workshop_creation_returns_a_resolving_location_and_preserves_the_utc_instant()
    {
        using var factory = new SeatFlowApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/workshops", new
        {
            title = "Offset workshop",
            startTime = "2030-04-16T09:00:00+02:00",
            endTime = "2030-04-16T10:00:00+02:00",
            capacity = 4
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var createdStart = created.RootElement.GetProperty("startTime").GetDateTimeOffset();
        var lookup = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        using var fetched = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        var fetchedStart = fetched.RootElement.GetProperty("startTime").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, createdStart.Offset);
        Assert.Equal(createdStart, fetchedStart);
        Assert.Equal(new DateTimeOffset(2030, 4, 16, 7, 0, 0, TimeSpan.Zero), fetchedStart);
    }

    [Fact]
    public async Task Registration_route_requires_idempotency_and_returns_a_resolving_location()
    {
        using var factory = new SeatFlowApiFactory();
        using var client = factory.CreateClient();
        using var workshopsResponse = await client.GetAsync("/api/workshops");
        using var workshops = JsonDocument.Parse(await workshopsResponse.Content.ReadAsStringAsync());
        var available = workshops.RootElement.EnumerateArray().First(item => item.GetProperty("title").GetString() == "A practical AI toolkit");
        var workshopId = available.GetProperty("id").GetGuid();
        using var participantsResponse = await client.GetAsync("/api/participants");
        using var participants = JsonDocument.Parse(await participantsResponse.Content.ReadAsStringAsync());
        var participantId = participants.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        var path = $"/api/workshops/{workshopId}/registrations";
        var missingKey = await client.PostAsJsonAsync(path, new { participantId });
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { participantId }) };
        request.Headers.Add("Idempotency-Key", "route-test-1");
        var created = await client.SendAsync(request);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        Assert.NotNull(created.Headers.Location);
        var resolved = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        using var duplicate = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { participantId }) };
        duplicate.Headers.Add("Idempotency-Key", "route-test-2");
        var conflict = await client.SendAsync(duplicate);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private sealed class SeatFlowApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"seatflow-api-{Guid.NewGuid():N}");
        private string DatabasePath => Path.Combine(_directory, "seatflow.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_directory);
            builder.UseContentRoot(_directory);
            builder.UseSetting("DatabasePath", DatabasePath);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabasePath"] = DatabasePath
            }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }
}
