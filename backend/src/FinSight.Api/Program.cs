using FinSight.Api.Middleware;
using FinSight.Data;
using FinSight.Services;
using FinSight.Services.Import;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------
// Priority: ConnectionStrings:Default -> FINSIGHT_CONNECTION_STRING -> in-memory fallback.
var configuredConnection = builder.Configuration.GetConnectionString("Default");
var connectionString = !string.IsNullOrWhiteSpace(configuredConnection)
    ? configuredConnection
    : Environment.GetEnvironmentVariable("FINSIGHT_CONNECTION_STRING");

// ---------- Services ----------
builder.Services.AddDbContext<FinSightDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
        options.UseNpgsql(connectionString);
    else
        options.UseInMemoryDatabase(
            builder.Configuration["FINSIGHT_INMEMORY_DB"]
            ?? Environment.GetEnvironmentVariable("FINSIGHT_INMEMORY_DB")
            ?? "finsight-dev-fallback");
});

builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<SeedService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "FinSight API",
        Version = "v1",
        Description = "Personal finance analytics API: transactions, CSV imports, budgets and dashboard metrics."
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("finsight-ui", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:4200", "https://localhost:4200"];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// ---------- Database ----------
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seed.EnsureDefaultCategoriesAsync();
}

// ---------- Pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("finsight-ui");
app.MapControllers();

app.Run();

public partial class Program;
