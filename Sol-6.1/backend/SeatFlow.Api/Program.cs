using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SeatFlow");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(builder.Environment.ContentRootPath, "seatflow.db"),
        DefaultTimeout = 30, ForeignKeys = true
    }.ToString();
builder.Services.AddDbContext<SeatFlowDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddSingleton<DevelopmentNotificationService>();
builder.Services.AddSingleton<INotificationService>(services => services.GetRequiredService<DevelopmentNotificationService>());
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapControllers();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SeatFlowDbContext>();
    if (builder.Configuration.GetValue("SeedData", true))
        await SeedData.InitializeAsync(db);
    else
        await db.Database.EnsureCreatedAsync();
}
await app.RunAsync();

public partial class Program;
