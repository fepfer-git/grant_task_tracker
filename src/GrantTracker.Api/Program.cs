using System.Text.Json.Serialization;
using GrantTracker.Api.Errors;
using GrantTracker.Api.Services;
using GrantTracker.Api.Startup;
using GrantTracker.Api.Time;
using GrantTracker.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Enum names only: a number such as 3 is rejected, not read as High.
// Request DTOs mark optional fields with '?', so the framework's implicit "required" check isn't needed; it would also
// add a misleading error for the whole body when one field can't be read.
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = InvalidInputResponse.Create);

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
builder.Services.AddScoped<GrantCommandService>();

// OP-5 health checks (DESIGN.md §6.4). Only readiness checks the database.
const string readyTag = "ready";
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: [readyTag]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapControllers();

// The body is the default writer's status word only (Healthy or Unhealthy), so no exception details or connection
// strings reach the caller. Unhealthy is a 503.
// Liveness runs no checks: a database outage must not make Kubernetes restart every copy of the API.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Readiness fails while the database can't be reached, which takes this copy out of the load balancer.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(readyTag) });

app.Run();
