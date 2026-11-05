using System.Security.Claims;
using Bookit.Api.Auth;
using Bookit.Application.Catalog;

namespace Bookit.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/businesses").WithTags("Catalog");

        // --- Owner only: everything that creates or lists "my" data ---

        group.MapPost("/", async (
            CreateBusinessRequest request,
            ClaimsPrincipal user,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var business = await catalog.CreateBusinessAsync(user.GetUserId(), request, cancellationToken);

            return Results.Created($"/api/businesses/{business.Id}", business);
        }).RequireAuthorization();

        group.MapGet("/mine", async (
            ClaimsPrincipal user,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListOwnedBusinessesAsync(user.GetUserId(), cancellationToken)))
            .RequireAuthorization();

        group.MapPost("/{businessId:guid}/services", async (
            Guid businessId,
            CreateServiceRequest request,
            ClaimsPrincipal user,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var service = await catalog.AddServiceAsync(user.GetUserId(), businessId, request, cancellationToken);

            return Results.Created($"/api/businesses/{businessId}/services", service);
        }).RequireAuthorization();

        group.MapPost("/{businessId:guid}/providers", async (
            Guid businessId,
            CreateProviderRequest request,
            ClaimsPrincipal user,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var provider = await catalog.AddProviderAsync(user.GetUserId(), businessId, request, cancellationToken);

            return Results.Created($"/api/businesses/{businessId}/providers", provider);
        }).RequireAuthorization();

        // --- Public: what a customer needs to see the booking page ---

        group.MapGet("/{businessId:guid}", async (
            Guid businessId,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
            Results.Ok(await catalog.GetBusinessAsync(businessId, cancellationToken)));

        group.MapGet("/{businessId:guid}/services", async (
            Guid businessId,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListServicesAsync(businessId, cancellationToken)));

        group.MapGet("/{businessId:guid}/providers", async (
            Guid businessId,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListProvidersAsync(businessId, cancellationToken)));

        return app;
    }
}