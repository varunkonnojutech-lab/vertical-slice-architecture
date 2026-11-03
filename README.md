# Vertical Slice Architecture in .NET — one feature, one folder, everything it needs

[![Build](https://github.com/varunkonnojutech-lab/vertical-slice-architecture/actions/workflows/ci.yml/badge.svg)](https://github.com/varunkonnojutech-lab/vertical-slice-architecture/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/varunkonnojutech-lab/vertical-slice-architecture)

Open a typical layered codebase and change one feature — say, how cancelling an order works. You touch the controller, the service, the DTO, the validator, maybe the repository interface, and you re-check every other feature that shares those files. The feature is smeared across horizontal layers, and every change is a negotiation with the rest of the codebase.

Vertical slice architecture flips the cut: organize by **feature**, not by technical layer. One folder per feature, and inside it everything that feature needs — its endpoint, its request and response shapes, its validation, its rules. Change "cancel order" and you touch one file. Delete a feature and you delete its folder; nothing else notices.

## Who is this for

.NET developers who have built a layered or Clean Architecture API (see the [clean-architecture-cqrs-template](https://github.com/varunkonnojutech-lab/clean-architecture-cqrs-template) repo for that shape) and felt the friction: a "simple" feature change rippling through five projects, shared DTOs that no single feature owns, and merge conflicts because every feature edits the same service file. If your team ships features, not layers, this is the layout that matches how you actually work.

## What you'll learn

- **Slices, not layers** — a feature's endpoint, validation, and handler live together in one file, so a feature is one unit of change, review, and deletion.
- **No shared DTOs** — `GetOrder` returns line items; `ListOrders` returns summaries. Each slice defines its own response shape, so neither is a compromise between two callers' needs. Shared DTOs are coupling in disguise.
- **Validation belongs to the feature** — the rules for creating an order (items required, quantities positive) live in `CreateOrder`, not in a shared validation library. When the rule changes, exactly one place changes.
- **What *is* shared** — only genuine infrastructure: the `OrdersStore` and the `Order` entity. Shared code should be boring plumbing, never business logic.
- **State rules as endpoint logic** — "only Pending orders can be cancelled" is enforced in `CancelOrder` with a `409 Conflict`, right next to the endpoint that needs it.
- **When *not* to slice** — vertical slices are a poor fit when many features share complex domain rules (that's where DDD aggregates earn their keep — see [ddd-tactical-patterns](https://github.com/varunkonnojutech-lab/ddd-tactical-patterns)), or when you need the strict dependency discipline of Clean Architecture across teams.

## Quick start

You need .NET 8. No Docker, no database, no external services — the store is in-memory:

```bash
dotnet test
dotnet run --project src/VerticalSlice.Api
```

The API listens on `http://localhost:5187` (or the port `dotnet run` prints).

## Try it

```bash
# 1. Create an order — the total is computed by the slice, not the caller
curl -X POST http://localhost:5187/orders -H 'Content-Type: application/json' -d '{
  "customer": "Ada Lovelace",
  "items": [
    { "product": "Mechanical keyboard", "quantity": 2, "unitPrice": 89.99 },
    { "product": "Mouse pad", "quantity": 1, "unitPrice": 19.99 }
  ]
}'
# → 201 Created: { "id": "...", "customer": "Ada Lovelace",
#     "total": 199.97, "status": "Pending", ... }

# 2. Fetch it back (note: this slice returns the line items)
curl http://localhost:5187/orders/<id>

# 3. List, filtered — list returns summaries, not the detail shape
curl "http://localhost:5187/orders?status=Pending"
curl "http://localhost:5187/orders?customer=ada"

# 4. Cancel it — then try again and watch the 409
curl -X POST http://localhost:5187/orders/<id>/cancel
curl -X POST http://localhost:5187/orders/<id>/cancel
# → 409 Conflict: { "error": "Order is Cancelled and cannot be cancelled." }

# 5. Validation lives in the slice — an empty order is a 400
curl -X POST http://localhost:5187/orders -H 'Content-Type: application/json' \
  -d '{"customer": "Ada", "items": []}'
# → 400 Bad Request: { "errors": ["At least one item is required."] }
```

## The same API, two layouts

The layered version of this API — the shape most tutorials teach:

```
src/
  Controllers/OrdersController.cs   ← every order feature, one file
  Services/OrderService.cs          ← every order rule, one file
  DTOs/OrderDto.cs                  ← one shape serving two callers
  Validators/OrderValidator.cs      ← every order rule, again
```

The vertical-slice version in this repo:

```
src/VerticalSlice.Api/
  Program.cs                        ← registers slices, nothing else
  Shared/
    Order.cs                        ← the entity every slice reads/writes
    OrdersStore.cs                  ← dumb storage, no rules
  Features/Orders/
    CreateOrder.cs                  ← endpoint + shapes + validation + rule
    GetOrder.cs                     ← endpoint + detail shape
    ListOrders.cs                   ← endpoint + summary shape + filters
    CancelOrder.cs                  ← endpoint + "only Pending cancels" rule
```

Now ask the question that matters: **where do I change "cancel order"?** In the layered version, the answer is "four files, and check what else they serve." Here, the answer is "`CancelOrder.cs`."

## The pieces

| File | Role |
|---|---|
| `src/VerticalSlice.Api/Program.cs` | Wires up the slices and the shared store; contains no feature logic |
| `src/VerticalSlice.Api/Shared/Order.cs` | The `Order` entity and its status lifecycle |
| `src/VerticalSlice.Api/Shared/OrdersStore.cs` | Thread-safe in-memory storage — plumbing only |
| `src/VerticalSlice.Api/Features/Orders/CreateOrder.cs` | `POST /orders`: validation, total computation, `201 Created` |
| `src/VerticalSlice.Api/Features/Orders/GetOrder.cs` | `GET /orders/{id}`: detail view with line items, `404` when missing |
| `src/VerticalSlice.Api/Features/Orders/ListOrders.cs` | `GET /orders`: status/customer filters, summary view |
| `src/VerticalSlice.Api/Features/Orders/CancelOrder.cs` | `POST /orders/{id}/cancel`: Pending-only rule, `409` on conflict |
| `tests/VerticalSlice.Tests/OrderApiTests.cs` | End-to-end HTTP tests, one slice at a time, fresh app per test |

## Rules of thumb for slicing well

- **One file per feature, until it hurts.** A slice that grows past ~200 lines is a sign it hides two features — split it. A slice shared by two features is a sign it hides shared infrastructure — extract it into `Shared`.
- **Duplication beats the wrong abstraction.** Two slices with similar-looking validation is fine; a shared validator that both must agree on is coupling. Let slices duplicate a little, and extract only when the duplication is proven, stable, and genuinely shared.
- **Slices talk through the store, not through each other.** `CancelOrder` never calls `CreateOrder`'s code; both go through `OrdersStore`. Cross-slice calls recreate the layering you just escaped.
- **Tests mirror slices.** Each test exercises one slice end-to-end over HTTP, with a fresh app per test — the same isolation the layout gives the code.

## Next steps

- Swap `OrdersStore` for EF Core behind the same shape, and watch how little changes: the slices don't care where orders live, only that the store holds them.
- Add a slice that needs a cross-cutting concern — say, an `ShipOrder` slice that publishes a domain event — and decide deliberately whether the event bus is `Shared` infrastructure or part of the slice.
- Add MediatR so each slice becomes a `IRequestHandler` instead of a static class, and compare: the folder layout stays the same, only the wiring changes. That's the point — vertical slices are an organization idea, not a library choice.
- Rebuild one feature of your own API this way and count the files a change touches before and after.
