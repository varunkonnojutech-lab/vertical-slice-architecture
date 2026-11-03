using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VerticalSlice.Tests;

/// <summary>
/// End-to-end tests through real HTTP, one slice at a time. Each test gets
/// a fresh app (and therefore a fresh OrdersStore), so slices never leak
/// state into each other — the same isolation the slice layout gives the code.
/// </summary>
public sealed class OrderApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>();
        return factory.CreateClient();
    }

    private static object NewOrder(string customer = "Ada Lovelace") => new
    {
        customer,
        items = new[]
        {
            new { product = "Mechanical keyboard", quantity = 2, unitPrice = 89.99m },
            new { product = "Mouse pad", quantity = 1, unitPrice = 19.99m }
        }
    };

    private static async Task<Guid> CreateOrderAsync(HttpClient client, object? body = null)
    {
        var response = await client.PostAsJsonAsync("/orders", body ?? NewOrder(), Json);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return created.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_computed_total()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/orders", NewOrder(), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(199.97m, body.GetProperty("total").GetDecimal()); // 2 × 89.99 + 19.99
    }

    [Fact]
    public async Task Create_rejects_an_order_with_no_items()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new { customer = "Ada", items = Array.Empty<object>() }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_a_non_positive_quantity()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new
        {
            customer = "Ada",
            items = new[] { new { product = "Keyboard", quantity = 0, unitPrice = 10m } }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_created_order_with_its_line_items()
    {
        using var client = CreateClient();
        var id = await CreateOrderAsync(client);

        var response = await client.GetAsync($"/orders/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal("Ada Lovelace", body.GetProperty("customer").GetString());
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_pending_order_succeeds_but_second_cancel_conflicts()
    {
        using var client = CreateClient();
        var id = await CreateOrderAsync(client);

        var first = await client.PostAsync($"/orders/{id}/cancel", null);
        var second = await client.PostAsync($"/orders/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task List_filters_by_status_and_customer()
    {
        using var client = CreateClient();
        var cancelledId = await CreateOrderAsync(client, NewOrder("Ada Lovelace"));
        await CreateOrderAsync(client, NewOrder("Grace Hopper"));
        await client.PostAsync($"/orders/{cancelledId}/cancel", null);

        var byStatus = await client.GetFromJsonAsync<JsonElement>("/orders?status=Cancelled", Json);
        var byCustomer = await client.GetFromJsonAsync<JsonElement>("/orders?customer=grace", Json);
        var all = await client.GetFromJsonAsync<JsonElement>("/orders", Json);

        Assert.Single(byStatus.EnumerateArray());
        Assert.Single(byCustomer.EnumerateArray());
        Assert.Equal(2, all.GetArrayLength());
    }
}
