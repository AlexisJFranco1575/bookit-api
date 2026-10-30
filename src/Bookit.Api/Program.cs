using System.Text.Json.Serialization;
using Bookit.Api;
using Bookit.Api.Auth;
using Bookit.Api.Endpoints;
using Bookit.Api.Errors;
using Bookit.Application.Auth;
using Bookit.Application.Booking;
using Bookit.Application.Catalog;
using Bookit.Domain.Scheduling;
using Bookit.Infrastructure;
using Bookit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as text ("Monday", "Booked"), never as numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

// The connection string is read when the DbContext is first needed, so tests and hosts can override it.
builder.Services.AddDbContext<BookitDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Bookit")
        ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Bookit'.");

    options.UseSqlServer(connectionString);
});

builder.Services.AddBookitInfrastructure();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SlotCalculator>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<BookingService>();

builder.Services.AddBookitAuthentication();
builder.Services.AddBookitRateLimiting();

// Browsers may call the API only from the origins listed in configuration (none by default).
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<BookitDbContext>().Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapBookingEndpoints();

app.Run();

// Lets the integration tests start the whole API in memory.
public partial class Program;