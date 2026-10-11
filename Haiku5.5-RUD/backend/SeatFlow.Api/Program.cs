using SeatFlow.Api;
using SeatFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSeatFlow(builder.Configuration.GetConnectionString("SeatFlow")
    ?? throw new InvalidOperationException("Connection string 'SeatFlow' is missing."));

var app = builder.Build();

await app.Services.InitializeSeatFlowAsync();

app.UseExceptionHandler();
app.MapSeatFlowApi();

app.Run();

// Exposed so the test project can host the app with WebApplicationFactory.
public partial class Program { }
