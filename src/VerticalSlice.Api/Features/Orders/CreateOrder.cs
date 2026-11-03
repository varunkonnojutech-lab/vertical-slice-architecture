using VerticalSlice.Api.Shared;

namespace VerticalSlice.Api.Features.Orders;

/// <summary>
/// Create an order — one vertical slice, end to end, in one file:
/// the endpoint, the request/response shapes, and the validation rules
/// that belong to this feature and no other.
/// </summary>
public static class CreateOrder
{
    // This slice's own shapes. Another slice may need a different view of
    // an order (ListOrders returns a summary) — it defines its own, and
    // neither slice breaks when the other changes its shape.
    public sealed record ItemRequest(string Product, int Quantity, decimal UnitPrice);
    public sealed record Request(string Customer, List<ItemRequest> Items);
    public sealed record Response(Guid Id, string Customer, decimal Total, string Status, DateTimeOffset CreatedAt);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/orders", Handle);

    public static IResult Handle(Request request, OrdersStore store)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return Results.BadRequest(new { errors });
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            Customer = request.Customer.Trim(),
            Items = request.Items
                .Select(item => new OrderItem(item.Product.Trim(), item.Quantity, item.UnitPrice))
                .ToArray()
        };
        store.Add(order);

        return Results.Created(
            $"/orders/{order.Id}",
            new Response(order.Id, order.Customer, order.Total, order.Status.ToString(), order.CreatedAt));
    }

    // Validation lives with the feature, not in a shared validator library:
    // the rules for creating an order are this feature's business rules.
    internal static List<string> Validate(Request request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Customer))
        {
            errors.Add("Customer is required.");
        }
        if (request.Items is null || request.Items.Count == 0)
        {
            errors.Add("At least one item is required.");
            return errors;
        }
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Product))
            {
                errors.Add("Every item needs a product name.");
            }
            if (item.Quantity <= 0)
            {
                errors.Add($"Quantity for '{item.Product}' must be greater than zero.");
            }
            if (item.UnitPrice < 0)
            {
                errors.Add($"Unit price for '{item.Product}' cannot be negative.");
            }
        }
        return errors;
    }
}
