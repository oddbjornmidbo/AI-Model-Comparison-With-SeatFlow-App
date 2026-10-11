using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;
using SeatFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<SeatFlowContext>(options =>
    options.UseSqlite("Data Source=seatflow.db"));

builder.Services.AddScoped<INotificationService, InMemoryNotificationService>();
builder.Services.AddScoped<RegistrationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SeatFlowContext>();
    db.Database.EnsureCreated();
    await SeedData(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

app.Run();

static async Task SeedData(SeatFlowContext db)
{
    if (db.Participants.Any() || db.Workshops.Any())
        return;

    var now = DateTime.UtcNow;

    var p1 = new Participant { Name = "Alice Johnson", Email = "alice@example.com" };
    var p2 = new Participant { Name = "Bob Smith", Email = "bob@example.com" };
    var p3 = new Participant { Name = "Carol Davis", Email = "carol@example.com" };
    var p4 = new Participant { Name = "David Wilson", Email = "david@example.com" };
    var p5 = new Participant { Name = "Eve Martinez", Email = "eve@example.com" };

    db.Participants.AddRange(p1, p2, p3, p4, p5);

    var w1 = new Workshop
    {
        Title = "ASP.NET Core Basics",
        StartTime = now.AddHours(1),
        EndTime = now.AddHours(3),
        Capacity = 2
    };
    var w2 = new Workshop
    {
        Title = "Advanced C#",
        StartTime = now.AddHours(2),
        EndTime = now.AddHours(4),
        Capacity = 3
    };
    var w3 = new Workshop
    {
        Title = "React Fundamentals",
        StartTime = now.AddHours(5),
        EndTime = now.AddHours(7),
        Capacity = 2
    };
    var w4 = new Workshop
    {
        Title = "Entity Framework Deep Dive",
        StartTime = now.AddHours(8),
        EndTime = now.AddHours(10),
        Capacity = 4
    };

    db.Workshops.AddRange(w1, w2, w3, w4);
    await db.SaveChangesAsync();
}
