# Evaluation report

## Summary

No blocking issue remained after this evaluation round. The project was reviewed as if an evaluator had cloned it from scratch, followed the README, started the local Docker stack, opened Swagger, authenticated, exercised the endpoints, checked idempotency, observed logs and metrics, and reviewed the code and tests.

## Evaluation fronts

### Functional coverage

- `POST /api/v1/auth/token` issues a JWT for local evaluation.
- `POST /api/v1/balances/adjustments` applies balance changes and initializes unknown users.
- `GET /api/v1/balances/{userId}` returns the current balance.
- `GET /api/v1/balances/{userId}/statement` returns the user statement and supports period filters.
- Replay with the same `operationId` does not apply the amount twice.
- Replay with the same `operationId` and different payload returns `409 Conflict`.
- Validation errors return `422 Unprocessable Entity`.

### Concurrency and consistency

- Balance updates run inside a database transaction.
- User balance rows are locked with PostgreSQL `FOR UPDATE`.
- Idempotency is enforced by a unique operation key and request hash.
- The consistency mechanism is database-centered, so it works across multiple API instances.

### Persistence and volume

- Current balances and immutable movements are persisted separately.
- Movements are indexed for user and time-window statement queries.
- Large-volume handling is documented with retention, partitioning, archiving, and index strategy.

### Observability

- Structured logs are available in Seq.
- Prometheus scrapes the API metrics endpoint.
- Grafana is included in the local stack.
- Request logs include HTTP method, route, status code, trace id, span id, service metadata, and elapsed time.

### Tests and evidence

- Build: `dotnet build .\BalanceControlApi.slnx` passed with `0` warnings and `0` errors.
- Automated tests: `./scripts/test.ps1 -SkipBuild` passed `25/25`.
- Coverage: `./scripts/coverage.ps1` passed with `98.65%` line coverage, above the `80%` target.
- Smoke test: `./scripts/smoke.ps1` passed with final balance `60.00`, two statement items, and replay not applied.
- Idempotency test: `./scripts/idempotency.ps1` passed with first application `true`, replay application `false`, same movement id `true`, and conflict status `409`.
- Spike test: `./scripts/spike-three-accounts.ps1 -Adjustments 90 -Concurrency 15` passed with `123` requests, `0` unexpected failures, p95 `72 ms`, p99 `107 ms`, and matching expected balances for all three accounts.
- Docker stack: `BalanceControl-local-api`, `BalanceControl-local-postgres`, `BalanceControl-local-seq`, `BalanceControl-local-prometheus`, and `BalanceControl-local-grafana` started successfully.

## Issues fixed during the audit

### Date-time truncation

The API had a global JSON converter that formatted every `DateTime` as a date-only value. That would make statement timestamps, movement timestamps, balance update times, and token expiration less useful to an evaluator.

Resolution:

- Removed the strict global `DateTime` converter.
- Restored ISO 8601 date-time handling for `occurredAt`, response timestamps, and idempotency hashing.
- Added a focused unit test around request hashing normalization.
- Updated payload documentation to use ISO 8601 UTC examples.

### Orphan API response filter

An unused response filter remained from the original base-controller structure.

Resolution:

- Removed the unused filter.
- Re-ran build, tests, coverage, smoke, and idempotency checks.

## Residual risks and trade-offs

- The local JWT secret exists only for the exercise and local Docker execution. A real environment should inject it through a secret manager or CI/CD secret store.
- There is no endpoint that automatically replays all operations from a period. Replay is intentionally modeled as resending original events with the same `operationId`, which is the behavior required by the exercise.
- The delivery folder is not a Git repository yet. Before submitting, initialize or copy it into a repository and grant read access as requested.
- Spike evidence under `artifacts/spike` is useful for review, but older spike runs may be trimmed before final submission if a smaller repository is preferred.

## Final readiness

The project is ready for a technical-test review round. The API covers the requested endpoints, consistency under concurrent updates is database-backed, replay/idempotency behavior is demonstrable, the Docker stack contains the required services under the `BalanceControl-local` naming, and the README plus supporting docs explain how to run, use, test, observe, and evaluate the solution.
