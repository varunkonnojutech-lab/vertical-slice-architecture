using VerticalSlice.Api.Shared;

namespace VerticalSlice.Api.Features.Orders;

/// <summary>
/// Cancel an order — and the clearest example of why slices beat layers
/// for feature rules: the "only Pending orders can be cancelled" rule
/// lives here, next to the endpoint that enforces it, not scattered
/// across a service layer and a controller.
/// </summary>
public static class CancelOrder
{
    public sealed record Response(Guid Id, string Status);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/orders/{id:guid}/cancel", Handle);

    public static IResult Handle(Guid id, OrdersStore store)
    {
        var order = store.Get(id);
        if (order is null)
        {
            return Results.NotFound(new { error = $"No order with id {id}." });
        }

        if (order.Status != OrderStatus.Pending)
        {
            // 409 Conflict: the request is well-formed, but the order's
            // current state makes it impossible. Shipped orders can't be
            // un-shipped, and cancelling twice is not an action.
            return Results.Conflict(new { error = $"Order is {order.Status} and cannot be cancelled." });
        }

        order.Status = OrderStatus.Cancelled;
        return Results.Ok(new Response(order.Id, order.Status.ToString()));
    }
}
