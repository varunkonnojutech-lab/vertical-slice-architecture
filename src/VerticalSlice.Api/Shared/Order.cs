namespace VerticalSlice.Api.Shared;

/// <summary>Order lifecycle. Statuses are deliberately boring: a slice may only
/// move an order along transitions its own rules allow (see CancelOrder).</summary>
public enum OrderStatus
{
    Pending,
    Shipped,
    Cancelled
}

public sealed record OrderItem(string Product, int Quantity, decimal UnitPrice);

/// <summary>The one shared domain object. Slices read and write it, but each
/// slice defines its own request/response shapes — never a shared DTO.</summary>
public sealed class Order
{
    public required Guid Id { get; init; }
    public required string Customer { get; init; }
    public required IReadOnlyList<OrderItem> Items { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public decimal Total => Items.Sum(item => item.Quantity * item.UnitPrice);
}
