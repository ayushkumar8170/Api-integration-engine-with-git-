# Automated REST API & System Integration Engine

A modular service that ingests JSON payloads, validates them against
JSON Schema contracts, and bridges (forwards) them to one or more
downstream REST endpoints under a strict per-request latency budget —
built to catch schema drift and upstream failures before they propagate
across distributed subsystems.

**Stack:** C# (.NET 8 / ASP.NET Core) · Python (pytest, Postman/Newman) ·
RESTful APIs · JSON Schema · Docker · GitHub Actions

---

## Highlights

- **Schema-validated ingestion** — every payload is validated against a
  JSON Schema contract (`NJsonSchema`) before it's allowed anywhere near
  a downstream call; validation failures return structured `400`
  responses instead of silently forwarding malformed data.
- **API-key authorization** — every `/api/*` request must carry a valid
  `X-Api-Key` header, checked before validation or bridging so an
  unauthenticated caller never burns latency budget or reaches a
  downstream system.
- **Latency-bounded bridging** — outbound calls to downstream endpoints
  run under a configurable per-request budget (default **130ms**),
  enforced with a `CancellationTokenSource` timeout rather than a
  best-effort HttpClient timeout, so a slow downstream can't blow the
  caller's SLA.
- **Centralized exception handling** — a single middleware translates
  validation errors, timeouts, and upstream failures into a consistent
  JSON error envelope with the right status code, instead of leaking
  stack traces or inconsistent shapes per-controller.
- **Two layers of automated testing** — C#/xUnit unit tests for the
  validation and bridging logic, plus a Python (`pytest` + `requests`)
  integration suite that exercises the running service end-to-end,
  including dedicated latency-budget and authorization tests. A Postman
  collection covers the same flows for manual/CI (`newman`) verification.
- **Container-first** — multi-stage `Dockerfile` and a `docker-compose.yml`
  that also spins up a stub downstream service, so `docker compose up`
  gives you a fully working local integration test environment with no
  external dependencies.

## Architecture

```
        POST /api/payloads/ingest
        Header: X-Api-Key
                 │
                 ▼
        ┌─────────────────┐
        │ ApiKeyAuthMiddleware │──> 401 if missing/invalid key
        └────────┬────────┘
                  │
                  ▼
        ┌─────────────────┐
        │ PayloadController│
        └────────┬────────┘
                  │ PayloadEnvelope
                  ▼
        ┌─────────────────────┐
        │ IPayloadValidator     │──> JSON Schema (Schemas/payload.schema.json)
        │ (JsonSchemaPayload-   │    validation errors -> 400, structured envelope
        │  Validator)           │
        └────────┬─────────────┘
                  │ validated payload
                  ▼
        ┌─────────────────────┐
        │ IEndpointBridge       │──> downstream REST endpoint(s), per
        │ (EndpointBridgeService)│    call bounded by LatencyBudgetMs
        └────────┬─────────────┘
                  │
                  ▼
        ExceptionHandlingMiddleware  — wraps the whole pipeline; converts
                                        validation / timeout / upstream
                                        exceptions into one JSON error shape
```

See [`docs/architecture.md`](docs/architecture.md) for the full request
lifecycle, the latency-budget design, and the error-envelope schema.

## Repository layout

```
api-integration-engine/
├── src/IntegrationEngine/
│   ├── Controllers/PayloadController.cs
│   ├── Services/
│   │   ├── IPayloadValidator.cs / JsonSchemaPayloadValidator.cs
│   │   └── IEndpointBridge.cs / EndpointBridgeService.cs
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   └── ApiKeyAuthMiddleware.cs
│   ├── Models/ (PayloadEnvelope, ValidationResult, BridgeResult, ErrorEnvelope)
│   ├── Exceptions.cs
│   ├── Configuration/IntegrationOptions.cs (+ AuthOptions)
│   ├── Schemas/payload.schema.json
│   ├── Program.cs, appsettings.json, Dockerfile
│   └── IntegrationEngine.csproj
├── stub-downstream/                  # minimal Flask stand-in for a downstream service
├── tests/
│   ├── IntegrationEngine.Tests/      # xUnit unit tests
│   └── python/                       # pytest integration test suite
├── postman/IntegrationEngine.postman_collection.json
├── docker-compose.yml
├── .github/workflows/ci.yml
└── docs/architecture.md
```

## Setup

### Run locally with .NET

```bash
cd src/IntegrationEngine
dotnet restore
dotnet run
# service listens on http://localhost:8080
```

### Run with Docker Compose (service + stub downstream)

```bash
docker compose up --build
```

This starts:
- `integration-engine` on `http://localhost:8080`
- `stub-downstream` — a lightweight echo/latency-injecting stub the
  engine bridges to, so the whole pipeline is testable with zero
  external dependencies.

By default Compose sets `Auth__ApiKey=local-compose-key`; override it
with `ENGINE_API_KEY=<your-key> docker compose up` if you want something
else. Every `/api/*` request (except the health checks) must include
`X-Api-Key: <that value>`.

## Configuration

`src/IntegrationEngine/appsettings.json`:

```json
{
  "Integration": {
    "LatencyBudgetMs": 130,
    "BridgeTargets": [
      {
        "id": "orders-service",
        "baseUrl": "http://stub-downstream:9090",
        "path": "/ingest",
        "timeoutMs": 100
      }
    ]
  }
}
```

- `LatencyBudgetMs` — the end-to-end budget enforced on the
  ingest → validate → bridge round trip.
- `BridgeTargets[].timeoutMs` — per-target timeout for the outbound
  call; should stay comfortably under `LatencyBudgetMs` to leave room
  for validation and serialization overhead.

## API

### `POST /api/payloads/ingest`

Requires header `X-Api-Key: <configured key>` (see `Auth:ApiKey` in
`appsettings.json` / `appsettings.Development.json`, or `ENGINE_API_KEY`
for Docker Compose).

```json
{
  "targetId": "orders-service",
  "schemaId": "payload.schema.json",
  "payload": {
    "orderId": "ORD-1029",
    "amount": 42.50,
    "currency": "USD"
  }
}
```

Success (`200`):
```json
{
  "status": "bridged",
  "targetId": "orders-service",
  "elapsedMs": 47,
  "downstreamStatusCode": 200
}
```

Validation failure (`400`):
```json
{
  "error": "schema_validation_failed",
  "message": "Payload does not conform to the configured schema.",
  "details": ["#/amount: Required property 'amount' not found"]
}
```

Latency budget exceeded (`504`):
```json
{
  "error": "latency_budget_exceeded",
  "message": "Downstream call exceeded the 130ms latency budget.",
  "targetId": "orders-service"
}
```

Missing/invalid API key (`401`):
```json
{
  "error": "unauthorized",
  "message": "Missing or invalid X-Api-Key header."
}
```

## Testing

**C# unit tests:**
```bash
cd tests/IntegrationEngine.Tests
dotnet test
```

**Python integration tests** (against a running instance, local or
Docker Compose):
```bash
cd tests/python
pip install -r requirements.txt
ENGINE_BASE_URL=http://localhost:8080 ENGINE_API_KEY=local-dev-key pytest -v
```

**Postman / Newman:**
```bash
newman run postman/IntegrationEngine.postman_collection.json \
  --env-var baseUrl=http://localhost:8080 \
  --env-var apiKey=local-dev-key
```

## CI/CD

`.github/workflows/ci.yml` runs on every push/PR:
1. `dotnet build` + `dotnet test` for the C# solution
2. Python integration suite against a `docker compose up`-started stack
3. Docker image build (pushed only on `main`, left as a stub step to
   wire up to your registry of choice)

## License

MIT — see [`LICENSE`](LICENSE).
