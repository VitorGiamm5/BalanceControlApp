# Testing Strategy

The test suite focuses on the exercise risks: sequence correctness, concurrency, replay, and smoke-level HTTP behavior.

## Unit tests

Project:

```text
tests/BalanceControl.UnitTests
```

Coverage:

- input normalization;
- validation;
- repository call contract.

Command:

```bash
dotnet test tests/BalanceControl.UnitTests/BalanceControl.UnitTests.csproj --filter FullyQualifiedName~Balances
```

## Integration tests

Project:

```text
tests/BalanceControl.IntegrationTests
```

These use PostgreSQL through Testcontainers and prove:

- new user balance initialization;
- movement persistence;
- replay does not duplicate the balance;
- same operation with different payload is rejected;
- concurrent updates to the same user keep the expected final balance;
- concurrent replays apply only once.

Command:

```bash
dotnet test tests/BalanceControl.IntegrationTests/BalanceControl.IntegrationTests.csproj --filter FullyQualifiedName~Balances
```

## Smoke test

Project:

```text
tests/BalanceControl.FunctionalTests
```

The smoke test boots the API with PostgreSQL/Testcontainers and executes:

1. first balance adjustment;
2. replay of the same operation;
3. second balance adjustment;
4. balance query;
5. statement query.

Command:

```bash
dotnet test tests/BalanceControl.FunctionalTests/BalanceControl.FunctionalTests.csproj --filter FullyQualifiedName~BalanceSmokeTests
```

## Stress and load

Use `scripts/stress.ps1` against a running API.

Important metrics:

- throughput;
- p95 and p99 latency;
- success/error rate;
- replay count;
- conflict count;
- PostgreSQL lock wait and deadlocks;
- connection pool saturation;
- statement query latency as movement volume grows.

## Three-account spike

Use:

```powershell
./scripts/spike-three-accounts.ps1 -Adjustments 900 -Concurrency 60
```

The script creates three batch-scoped accounts, seeds them, interleaves credits, debits and balance queries, executes intentional replays, validates every response, compares final balances and movement totals, and captures API/PostgreSQL logs under `artifacts/spike`.

## Coverage gate

Use:

```powershell
./scripts/coverage.ps1
```

The script runs the test suite with OpenCover output and fails when aggregated line coverage of the balance exercise core is below 80%. The measured target is the HTTP balance controller, balance application services, and balance repository.
