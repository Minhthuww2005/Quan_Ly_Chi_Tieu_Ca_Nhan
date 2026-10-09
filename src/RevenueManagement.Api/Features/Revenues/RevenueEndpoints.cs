namespace RevenueManagement.Api.Features.Revenues;

// TODO(A): Minimal API CRUD /api/revenues.
public static class RevenueEndpoints
{
    public static void MapRevenueEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/revenues").RequireAuthorization().WithTags("Revenues");
        routes.MapGet("/", async (long groupId, long? walletId, DateOnly? from, DateOnly? to,
            RevenueService service, Infrastructure.Authorization.CurrentUser user, CancellationToken ct, int page = 1, int pageSize = 50) =>
            Results.Ok(await service.ListAsync(user.UserId!.Value, groupId, walletId, from, to, page, pageSize, ct)));
        routes.MapGet("/{id:long}", async (long id, RevenueService service, Infrastructure.Authorization.CurrentUser user, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(user.UserId!.Value, id, ct)));
        routes.MapPost("/", async (CreateRevenueRequest request, RevenueService service, Infrastructure.Authorization.CurrentUser user, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(user.UserId!.Value, request, ct);
            return Results.Created($"/api/revenues/{result.Id}", result);
        });
        routes.MapPut("/{id:long}", async (long id, UpdateRevenueRequest request, RevenueService service, Infrastructure.Authorization.CurrentUser user, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(user.UserId!.Value, id, request, ct)));
        routes.MapDelete("/{id:long}", async (long id, RevenueService service, Infrastructure.Authorization.CurrentUser user, CancellationToken ct) =>
        { await service.DeleteAsync(user.UserId!.Value, id, ct); return Results.NoContent(); });
    }
}
