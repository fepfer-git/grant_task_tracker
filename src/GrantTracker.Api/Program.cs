using System.Text.Json.Serialization;
using GrantTracker.Api.Errors;
using GrantTracker.Api.Services;
using GrantTracker.Api.Startup;
using GrantTracker.Api.Time;
using GrantTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// One ProblemDetails shape for every error (DESIGN.md §5.1).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemTypes.ApplyDefaults);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Time (DESIGN.md §4.0). An unknown App:TimeZoneId stops the app at start-up instead of on the first request.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .Validate(options => options.HasKnownTimeZone(), $"{AppOptions.SectionName}:TimeZoneId is not a known time zone id.")
    .ValidateOnStart();
builder.Services.AddSingleton<IAppClock, AppClock>();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not set.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddScoped<GrantQueryService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapControllers();

app.Run();
