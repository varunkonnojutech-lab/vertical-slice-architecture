using VerticalSlice.Api.Shared;

namespace VerticalSlice.Api.Features.Orders;

/// <summary>Fetch one order by id. Its response shape is this slice's own —
/// note it includes the line items, which ListOrders deliberately omits.</summary>
public static class GetOrder
{
    public sealed record ItemResponse(string Product, int Quantity, decimal UnitPrice);
    public sealed record Response(
        Guid Id, string Customer, List<ItemResponse> Items,
        decimal Total, string Status, DateTimeOffset CreatedAt);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/orders/{id:guid}", Handle);

    public static IResult Handle(Guid id, OrdersStore store)
    {
        var order = store.Get(id);
        if (order is null)
        {
            return Results.NotFound(new { error = $"No order with id {id}." });
        }

        return Results.Ok(new Response(
            order.Id,
            order.Customer,
            order.Items.Select(item => new ItemResponse(item.Product, item.Quantity, item.UnitPrice)).ToList(),
            order.Total,
            order.Status.ToString(),
            order.CreatedAt));
    }
}
