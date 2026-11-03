using VerticalSlice.Api.Shared;

namespace VerticalSlice.Api.Features.Orders;

/// <summary>List orders, optionally filtered. The summary shape here is
/// intentionally different from GetOrder's detail shape — each slice
/// serves its own callers, so neither shape is a compromise between them.</summary>
public static class ListOrders
{
    public sealed record Response(Guid Id, string Customer, decimal Total, string Status);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/orders", Handle);

    public static IResult Handle(string? status, string? customer, OrdersStore store)
    {
        var orders = store.All().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            orders = orders.Where(order => order.Status == parsedStatus);
        }
        // An unrecognized status value filters nothing rather than erroring:
        // listing is a read, and a typo should not become a 400 for callers.

        if (!string.IsNullOrWhiteSpace(customer))
        {
            orders = orders.Where(order =>
                order.Customer.Contains(customer, StringComparison.OrdinalIgnoreCase));
        }

        return Results.Ok(orders
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new Response(order.Id, order.Customer, order.Total, order.Status.ToString()))
            .ToArray());
    }
}
