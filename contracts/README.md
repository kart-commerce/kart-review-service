# contracts/

Vendored, read-only copies of this service's approved API/event contracts, synced from
`kart-shared/contracts/kart-review-service/` (the platform's source of truth) — never hand-edited
here. `message-bus-manifest.json` is authored directly in this repo (kart-shared only carries the
API/event contract prose, not the strongly-typed RabbitMQ topology JSON `Kart.Shared.Messaging`
consumes at runtime) — its `publishedEvents`/routing keys/DLQ names are derived directly from
`event-contract.md`'s approved tables.

- `api-contract.yaml` — OpenAPI 3.0.3, all 6 endpoints (`POST/GET /v1/reviews`, `PATCH/DELETE
  /v1/reviews/{id}`, `PATCH /v1/reviews/{id}/moderate`, `GET /v1/product-ratings/{sku}`).
- `event-contract.md` — published (`ReviewSubmitted`/`ReviewUpdated`/`ReviewUnpublished`) and
  consumed (`OrderCreated`/`OrderDelivered`) events.
- `message-bus-manifest.json` — the single source of truth for this service's entire RabbitMQ
  topology (exchanges, queues, bindings, retry ladders, DLQs). Nothing it describes is hardcoded
  in C# — `RabbitMqTopologyProvisioner` (from `Kart.Shared.Messaging`) declares it all at runtime.
