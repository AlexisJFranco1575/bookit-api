using System.Security.Claims;
using Bookit.Api.Auth;
using Bookit.Application.Agenda;
using Microsoft.AspNetCore.Mvc;

namespace Bookit.Api.Endpoints;

public static class AgendaEndpoints
{
    public static IEndpointRouteBuilder MapAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/businesses/{businessId:guid}/appointments", async (
            Guid businessId,
            [FromQuery(Name = "from")] DateOnly fromDate,
            [FromQuery(Name = "to")] DateOnly toDate,
            ClaimsPrincipal user,
            AgendaService agenda,
            CancellationToken cancellationToken) =>
            Results.Ok(await agenda.GetAgendaAsync(user.GetUserId(), businessId, fromDate, toDate, cancellationToken)))
            .WithTags("Agenda")
            .RequireAuthorization();

        return app;
    }
}