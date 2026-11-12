using GrantTracker.Api.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Time (DESIGN.md §4.0). An unknown App:TimeZoneId stops the app at start-up instead of on the first request.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .Validate(options => options.HasKnownTimeZone(), $"{AppOptions.SectionName}:TimeZoneId is not a known time zone id.")
    .ValidateOnStart();
builder.Services.AddSingleton<IAppClock, AppClock>();

var app = builder.Build();

app.MapControllers();

app.Run();
