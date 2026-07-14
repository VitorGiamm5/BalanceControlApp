# Architecture

Balance Control API is intentionally small: one HTTP API, one PostgreSQL database, and one bounded context for balances.

## Components

```text
Client / Swagger / Smoke
        |
        v
BalanceControl.Api
        |
        v
BalanceControl.Application
        |
        v
BalanceControl.Infrastructure
        |
        v
PostgreSQL
```

## Projects

| Project | Responsibility |
|---|---|
| `BalanceControl.Api` | HTTP endpoints, Swagger, JWT, middleware, API envelope |
| `BalanceControl.Application` | request validation, request normalization, use-case orchestration |
| `BalanceControl.Domain` | entities, DTOs, repository and service contracts |
| `BalanceControl.Infrastructure` | EF Core mapping, PostgreSQL access, migrations, resilience |
| `BalanceControl.Observability` | structured logging and OpenTelemetry setup |
| `BalanceControl.ServiceDefaults` | shared ASP.NET health and service defaults |

## Endpoint shape

The API exposes exactly the exercise surface:

- `POST /api/v1/balances/adjustments`
- `GET /api/v1/balances/{userId}`
- `GET /api/v1/balances/{userId}/statement`

No authentication or authorization is implemented because the exercise explicitly does not require it.

## Design principle

The database is the consistency boundary. Cache is not used for correctness. The current balance is materialized for fast reads, and the ledger keeps the operation history.
