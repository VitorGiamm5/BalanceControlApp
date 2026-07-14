# Operations

## Local stack

For a complete first-run guide from clone to Swagger, PostgreSQL and observability tool access, see:

```text
docs/how-to-run.md
```

Docker Compose project name:

```text
BalanceControl-local
```

Docker Compose normalizes the internal project identifier to `balancecontrol-local`. The local containers, network, and volume are explicitly named with the `BalanceControl-local` prefix.

The stack includes the API, PostgreSQL, Seq, Prometheus, Grafana, a dedicated network, and local data volumes.

Start API and PostgreSQL:

```powershell
./scripts/compose-up.ps1 -Build
```

Stop:

```powershell
./scripts/compose-down.ps1
```

Reset local data:

```powershell
./scripts/compose-down.ps1 -Volumes
```

## Swagger

```text
http://localhost:9005/swagger
```

## Health

```text
http://localhost:9005/health
```

## Smoke

After the stack is running:

```powershell
./scripts/smoke.ps1
```

## Stress

Example:

```powershell
./scripts/stress.ps1 -Users 10 -Operations 1000 -Concurrency 50 -ReplayRatio 0.10
```

## Configuration

Important environment variables:

| Variable | Purpose |
|---|---|
| `Kestrel__Port` | API port |
| `ConnectionStrings__PostgresWrite` | primary PostgreSQL connection |
| `ConnectionStrings__PostgresRead` | read PostgreSQL connection |
| `DatabaseSettings__RunMigrationsOnStartup` | applies migrations on startup when `true` |
| `ApiCors__AllowedOrigins` | CORS allowed origins |
| `Jwt__SigningKey` | JWT HMAC signing key |
| `Jwt__ClientId` | local token client id |
| `Jwt__ClientSecret` | local token client secret |
| `SEQ_URL` | Seq ingestion URL for structured logs |

## Runtime notes

- The API is stateless.
- Multiple API instances can process requests concurrently.
- Correctness depends on PostgreSQL constraints and transactions.
- No session affinity is required.
- For production-like execution, use managed secrets rather than plain compose environment variables.

## Observability URLs

| Tool | URL |
|---|---|
| Seq logs | `http://localhost:8081` |
| Prometheus | `http://localhost:9090` |
| Grafana | `http://localhost:3000` |

Grafana local credentials are `admin / admin`. Seq runs without authentication in the local Compose stack.

## PostgreSQL local access

| Setting | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| Database | `balance_control` |
| User | `balance_app` |
| Password | `balance_app` |
| Schema | `balance_control` |

Container access:

```powershell
docker exec -it BalanceControl-local-postgres psql -U balance_app -d balance_control
```
