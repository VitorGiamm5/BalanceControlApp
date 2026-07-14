# Balance Control API

API for controlling user balances, focused on consistency under concurrency and idempotent event replay.

## Requirements

- .NET SDK 10
- Docker, to run PostgreSQL locally or the full stack through Docker Compose
- PowerShell 7 to run the concurrent stress script

## Run With Docker Compose

The local stack is declared as `BalanceControl-local` and includes the API, PostgreSQL, a dedicated network, and the local database volume. Docker Compose normalizes the internal project name to `balancecontrol-local`, but local resources use the explicit `BalanceControl-local` prefix.

Complete guide from clone to Swagger, PostgreSQL, Seq, Prometheus, and Grafana access:

```text
docs/how-to-run.md
```

Manual API usage guide with ready-to-use payloads:

```text
docs/how-to-use.md
```

Readiness report after evaluator simulation:

```text
docs/evaluation-report.md
```

```bash
docker compose up --build
```

Swagger:

```text
http://localhost:9005/swagger
```

Local observability:

```text
Seq logs:   http://localhost:8081
Prometheus: http://localhost:9090
Grafana:    http://localhost:3000
```

Health:

```text
http://localhost:9005/health
```

## Run Locally

Start only PostgreSQL:

```bash
docker compose up postgres -d
```

Run the API:

```bash
dotnet run --project src/BalanceControl.Api/BalanceControl.Api.csproj
```

Migrations are applied on startup by default.

## Scripts

The main commands are also available under `scripts/`:

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/coverage.ps1
./scripts/compose-up.ps1 -Build
./scripts/smoke.ps1
./scripts/idempotency.ps1
./scripts/spike-three-accounts.ps1 -Adjustments 900 -Concurrency 60
./scripts/stress.ps1 -Users 10 -Operations 1000 -Concurrency 50 -ReplayRatio 0.10
./scripts/compose-down.ps1
```

## Endpoints

Balance endpoints require a JWT Bearer token. Generate a local token:

```http
POST /api/v1/auth/token
Content-Type: application/json

{
  "clientId": "balance-client",
  "clientSecret": "balance-secret"
}
```

Then send:

```http
Authorization: Bearer <accessToken>
```

### Adjust Balance

```http
POST /api/v1/balances/adjustments
Content-Type: application/json

{
  "userId": "user-0001",
  "operationId": "11111111-1111-4111-8111-111111111111",
  "amount": 100.50,
  "description": "initial credit"
}
```

`operationId` identifies the business event. Repeating the same operation with the same payload returns the previous result without applying the balance change again. Reusing the same `operationId` for the same user with a different payload returns `409 Conflict`.

The field-by-field payload contract is available at `docs/api-payloads.md`.

### Get Balance

```http
GET /api/v1/balances/user-0001
```

### Get Statement

```http
GET /api/v1/balances/user-0001/statement?page=1&pageSize=50
```

The statement is paginated. `pageSize` is limited to 200.

## Technical Decisions

### Balance Update

Balance adjustment is transactional. The API records the operation, updates the user's materialized balance, and writes the movement with the resulting balance. A missing user is initialized automatically on the first adjustment.

Queries for missing users return `404 Not Found`.

### Persistence

PostgreSQL is the source of truth. The model uses:

- `tb_user_balance`: current balance per user.
- `tb_balance_movement`: movement ledger.

The ledger has a unique constraint on `(user_id, operation_id)` and an index for statement reads by user.

### Concurrency

Balance updates use `INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING` in PostgreSQL. This makes the database serialize concurrent changes for the same user balance row while keeping parallelism between different users.

### Idempotency And Replay

Idempotency is durable in the database, not stored in cache. The operation is identified by `(userId, operationId)` and also stores a canonical hash of the payload. With this approach:

- replaying the same event does not duplicate the effect;
- concurrent replay applies at most once;
- the same key with a different payload creates a conflict;
- multiple API instances share the same guarantee through PostgreSQL.

### High Volume

The current balance is materialized in `tb_user_balance`, avoiding recomputing the balance from the ledger on every query. The statement is paginated and indexed by user/date. For larger volumes, the natural evolution is to partition `tb_balance_movement` by date and keep retention/archive policies for older movements.

## Tests

Build:

```bash
dotnet build BalanceControlApi.slnx
```

Unit tests:

```bash
dotnet test tests/BalanceControl.UnitTests/BalanceControl.UnitTests.csproj --filter FullyQualifiedName~Balances
```

Integration tests with PostgreSQL/Testcontainers:

```bash
dotnet test tests/BalanceControl.IntegrationTests/BalanceControl.IntegrationTests.csproj --filter FullyQualifiedName~Balances
```

HTTP smoke:

```bash
dotnet test tests/BalanceControl.FunctionalTests/BalanceControl.FunctionalTests.csproj --filter FullyQualifiedName~BalanceSmokeTests
```

The smoke test runs the main flow: adjust balance, repeat the same operation, validate that it was not duplicated, apply a new adjustment, get the balance, and get the statement.

Explicit idempotency validation:

```powershell
./scripts/idempotency.ps1
```

This script uses the real `POST /api/v1/balances/adjustments` endpoint: it applies an operation, repeats the same payload, attempts to reuse the same `operationId` with a different payload, gets the balance, and verifies that the statement has only one applied movement.

Coverage with an 80% minimum gate on the exercise core: balance controller, balance services, and balance repository.

```powershell
./scripts/coverage.ps1
```

## Stress And Load Test

For stress testing, I would run spikes with many concurrent requests against the same `userId`, validating:

- expected final balance;
- total applied movements;
- replay count;
- `409` conflicts;
- p95/p99 latency;
- lock waits/deadlocks in PostgreSQL;
- connection pool usage.

For load testing, I would keep sustained load with many users and a mix of new operations, replays, and queries. The main metrics would be throughput, error rate, latency per endpoint, database CPU/IO, active connections, ledger growth, and statement query time.

For an operational spike with three highly active accounts:

```powershell
./scripts/spike-three-accounts.ps1 -Adjustments 900 -Concurrency 60
```

The script interleaves credits, debits, and balance queries, executes intentional replays, validates responses, checks final balance/statement per account, and captures API/PostgreSQL logs.

## Additional Documentation

- `docs/architecture.md`: components and responsibilities.
- `docs/evaluation-rubric.md`: strict checklist to simulate a human evaluation from scratch.
- `docs/how-to-run.md`: clone, Docker, Swagger, token, PostgreSQL, and local tools.
- `docs/how-to-use.md`: manual sequence with token, credit, replay, debit, balance, and statement.
- `docs/api-payloads.md`: payloads, required fields, and validations.
- `docs/authentication.md`: simple local JWT and usage in Swagger/scripts.
- `docs/observability.md`: logs in Seq, metrics in Prometheus, and Grafana dashboard.
- `docs/persistence.md`: data model and evolution for volume.
- `docs/concurrency-idempotency.md`: concurrency, replay, and conflicts.
- `docs/testing-strategy.md`: functional tests, stress/load, and metrics.
- `docs/operations.md`: local execution, smoke, stress, and configuration.
