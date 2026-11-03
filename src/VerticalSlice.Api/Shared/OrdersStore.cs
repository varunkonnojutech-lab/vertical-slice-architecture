using System.Collections.Concurrent;

namespace VerticalSlice.Api.Shared;

/// <summary>Dumb storage, nothing more. It knows how to keep orders and hand
/// them back; every rule about orders lives in the feature slices.</summary>
public sealed class OrdersStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Add(Order order) => _orders[order.Id] = order;

    public Order? Get(Guid id) => _orders.TryGetValue(id, out var order) ? order : null;

    public IReadOnlyCollection<Order> All() => _orders.Values.ToArray();
}
