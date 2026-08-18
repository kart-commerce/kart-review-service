# kart-review-service

Review authorship (submission, edit, retraction), the two-stage automated + human moderation
workflow, and the canonical product rating aggregate (`ProductRating`, ADR-0014) for the Kart
platform. Built to the approved spec in `kart-platform/docs/services/kart-review-service/`.

## Architecture

- **.NET 8**, Clean Architecture (`Domain → Application → Infrastructure → Api`), CQRS/vertical
  slices under `src/Application/Features/`.
- **Write DB**: PostgreSQL (EF Core) — source of truth for `Review`, `ProductRating`,
  `VerifiedPurchaseRecord`, `review_outbox`, `idempotency_keys`, `audit_log`. Row-Level Security +
  a status-transition guard trigger, per `database-design.md`.
- **Read DB**: MongoDB (`review_read_model`) — kept in sync by a background projector that
  rebuilds each document from current Postgres state (never trusts the outbox payload directly).
- **Messaging**: RabbitMQ, topology declared entirely from `contracts/message-bus-manifest.json`
  (`Kart.Shared.Messaging`) — no hardcoded exchange/queue/routing-key strings in C#.
- **DDD**: `Review` and `ProductRating` aggregates (`Kart.Shared.Domain.AggregateRoot` /
  `OutboxEventBase`), strongly-typed IDs and value objects (`ReviewId`, `OrderId`, `UserId`, `Sku`,
  `Rating`, `ModerationStatus`) to avoid primitive obsession.
- **Idempotency** (defense in depth): `Idempotency-Key` header + `idempotency_keys` table (a new
  generic `IdempotencyBehaviour<,>` MediatR pipeline behavior) + `UNIQUE (order_id, sku)` DB
  constraint + the `product_rating_ledger` event-dedup ledger + the status-guard trigger.
- **Audit logging**: `Kart.Shared.Auditing` with a real EF-backed `audit_log` sink.
- **Global exception handling**: `Kart.Shared.ErrorHandling` — one `ProblemDetails` envelope
  platform-wide.
- **Observability**: `Kart.Shared.Observability` (Serilog + OpenTelemetry) + checkpoint-logging
  taxonomy.
- **Config**: `Kart.Shared.Configuration` GlobalConfig layering.

See `contracts/` for the API/event contracts and RabbitMQ topology manifest.

## Tickets implemented

All 11 approved backlog tickets (`REV-1` .. `REV-11`) from
`docs/services/kart-review-service/tickets.md` are implemented — see the code comments on each
feature slice / hosted service for the exact ticket it satisfies.

## Running locally

```bash
docker compose up --build
```

This starts Postgres, MongoDB, RabbitMQ, runs EF migrations via a one-shot `review-migrate`
container, then boots the API on `http://localhost:8095` (override with `REVIEW_PORT`). Swagger UI
is available at `/swagger` when `ASPNETCORE_ENVIRONMENT=Development` (the compose file's default).

`DevSeed:Enabled=true` in compose seeds one delivered order/SKU pair
(`orderId=11111111-1111-1111-1111-111111111111`, `sku=DEV-SKU-1`,
`userId=22222222-2222-2222-2222-222222222222`) so you can submit a review immediately without
hand-crafting `OrderCreated`/`OrderDelivered` events first.

Auth: bearer JWT validated against kart-identity-service's JWKS endpoint (`Identity:JwksUri`).
Moderation endpoints require a `roles` claim of `support_agent` or `admin`.

### Bare-metal dev

```bash
cp src/Api/appsettings.Local.json.example src/Api/appsettings.Local.json   # point at your kart-internals/globalconfig.json
scripts/migrate.sh                                                         # apply migrations (needs REVIEW_DB_CONNECTION_STRING / .env)
dotnet run --project src/Api/Kart.Review.Api.csproj
```

## Testing

```bash
dotnet test tests/UnitTests/Kart.Review.UnitTests.csproj             # pure domain/handler logic
dotnet test tests/IntegrationTests/Kart.Review.IntegrationTests.csproj  # real Postgres+Mongo+RabbitMQ via Testcontainers
dotnet test tests/ContractTests/Kart.Review.ContractTests.csproj     # api-contract.yaml shape validation
```

Integration tests require Docker and cover: the verified-purchase gate, the moderation
defer-until-outcome branching, idempotent replay/conflict, a 10-way concurrent-duplicate-request
race test, the Mongo read-model projector, the `ProductRating` self-consumer, and a real
end-to-end RabbitMQ test that publishes `OrderCreated`/`OrderDelivered` onto the broker directly.

## Out of scope

GDPR/`UserDataErased` erasure handling (ADR-0016) is explicitly flagged in the approved backlog as
requiring its own future requirement-spec pass — not ticketed, not implemented here.
