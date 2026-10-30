using Bookit.Application.Booking;
using Microsoft.AspNetCore.Mvc;

namespace Bookit.Api.Endpoints;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/availability", async (
            [FromQuery] Guid providerId,
            [FromQuery] Guid serviceId,
            [FromQuery(Name = "from")] DateOnly fromDate,
            [FromQuery(Name = "to")] DateOnly toDate,
            BookingService booking,
            CancellationToken cancellationToken) =>
            Results.Ok(await booking.GetAvailabilityAsync(providerId, serviceId, fromDate, toDate, cancellationToken)))
            .WithTags("Booking");

        var appointments = app.MapGroup("/api/appointments").WithTags("Booking");

        appointments.MapPost("/", async (
            BookAppointmentRequest request,
            BookingService booking,
            CancellationToken cancellationToken) =>
        {
            var appointment = await booking.BookAsync(request, cancellationToken);

            return Results.Created($"/api/appointments/{appointment.Id}", appointment);
        }).RequireRateLimiting(RateLimitPolicies.Booking);

        // The appointment id is a random Guid: whoever holds it (the customer) can view or cancel the appointment.
        appointments.MapGet("/{appointmentId:guid}", async (
            Guid appointmentId,
            BookingService booking,
            CancellationToken cancellationToken) =>
            Results.Ok(await booking.GetAsync(appointmentId, cancellationToken)));

        appointments.MapPost("/{appointmentId:guid}/cancel", async (
            Guid appointmentId,
            BookingService booking,
            CancellationToken cancellationToken) =>
            Results.Ok(await booking.CancelAsync(appointmentId, cancellationToken)))
            .RequireRateLimiting(RateLimitPolicies.Booking);

        return app;
    }
}