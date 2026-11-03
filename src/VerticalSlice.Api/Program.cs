using System.Text.Json.Serialization;
using VerticalSlice.Api.Features.Orders;
using VerticalSlice.Api.Shared;

var builder = WebApplication.CreateBuilder(args);

// Enums serialize as readable strings ("Pending") instead of numbers (0).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The ONLY shared service: dumb in-memory storage. No shared business logic,
// no shared DTOs, no shared validation — each slice owns its own.
builder.Services.AddSingleton<OrdersStore>();

var app = builder.Build();

app.MapGet("/", () => "Vertical Slice Architecture demo — see the README for the endpoints.");

CreateOrder.Map(app);
GetOrder.Map(app);
ListOrders.Map(app);
CancelOrder.Map(app);

app.Run();

// Lets WebApplicationFactory<Program> in the test project host this app.
public partial class Program;
