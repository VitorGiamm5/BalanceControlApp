# How to run

Este guia descreve o caminho do zero para executar a API, acessar o Swagger, autenticar as chamadas e entrar nas ferramentas locais da stack Docker.

## 1. Clonar o projeto

```bash
git clone <repository-url>
cd BalanceControlApi
```

Substitua `<repository-url>` pela URL do repositorio entregue para avaliacao.

## 2. Pre-requisitos

Instale:

| Ferramenta | Uso |
|---|---|
| Docker Desktop | Executar API, PostgreSQL, Seq, Prometheus e Grafana |
| .NET SDK 10 | Build e testes locais fora do container |
| PowerShell 7 | Scripts `scripts/*.ps1` |
| Git | Clone do repositorio |

Verificacao rapida:

```powershell
docker --version
docker compose version
dotnet --version
pwsh --version
git --version
```

## 3. Subir a stack completa com Docker

Opcao recomendada:

```powershell
./scripts/compose-up.ps1 -Build
```

Com Docker Compose puro:

```powershell
docker compose up --build -d
```

Verifique os containers:

```powershell
docker compose ps
```

A stack local usa o nome:

```text
BalanceControl-local
```

Containers esperados:

| Servico | Container | Porta local |
|---|---|---:|
| API | `BalanceControl-local-api` | `9005` |
| PostgreSQL | `BalanceControl-local-postgres` | `5432` |
| Seq | `BalanceControl-local-seq` | `8081`, `5341` |
| Prometheus | `BalanceControl-local-prometheus` | `9090` |
| Grafana | `BalanceControl-local-grafana` | `3000` |

## 4. Health check

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:9005/health
```

Resposta esperada:

```text
Healthy
```

## 5. Swagger

Abra:

```text
http://localhost:9005/swagger
```

Para uma sequencia manual completa com payloads prontos de credito, replay, debito, saldo e extrato, veja:

```text
docs/how-to-use.md
```

Para chamar os endpoints de saldo no Swagger:

1. Execute `POST /api/v1/auth/token`.
2. Use o `accessToken` retornado.
3. Clique em `Authorize`.
4. Informe o token no formato:

```text
Bearer <accessToken>
```

Credenciais locais para gerar token:

| Campo | Valor |
|---|---|
| `clientId` | `balance-client` |
| `clientSecret` | `balance-secret` |

Payload:

```json
{
  "clientId": "balance-client",
  "clientSecret": "balance-secret"
}
```

## 6. Smoke test

Com a stack rodando:

```powershell
./scripts/smoke.ps1
```

O script gera token JWT, aplica saldo, executa replay idempotente, consulta saldo e consulta extrato.

## 7. Acesso ao PostgreSQL

Credenciais locais:

| Campo | Valor |
|---|---|
| Host | `localhost` |
| Porta | `5432` |
| Database | `balance_control` |
| Usuario | `balance_app` |
| Senha | `balance_app` |
| Schema | `balance_control` |

Via `psql` dentro do container:

```powershell
docker exec -it BalanceControl-local-postgres psql -U balance_app -d balance_control
```

Consultas uteis:

```sql
select user_id, balance, version, updated_at
from balance_control.tb_user_balance
order by updated_at desc;

select user_id, operation_id, amount, balance_after, created_at, description
from balance_control.tb_balance_movement
order by created_at desc
limit 20;
```

Para DBeaver, DataGrip, pgAdmin ou outro cliente SQL, use as mesmas credenciais da tabela acima.

## 8. Observabilidade

| Ferramenta | URL | Login |
|---|---|---|
| Seq logs | `http://localhost:8081` | Sem autenticacao local |
| Prometheus | `http://localhost:9090` | Sem autenticacao local |
| Grafana | `http://localhost:3000` | `admin / admin` |
| API metrics | `http://localhost:9005/metrics` | Sem autenticacao local |

Dashboard provisionado no Grafana:

```text
Balance Control / Balance Control Overview
```

Busca util no Seq:

```text
RequestPath like '/api/v1/balances%'
```

Prometheus target:

```text
http://localhost:9090/targets
```

O target `balance-control-api` deve aparecer como `UP`.

## 9. Rodar testes locais

Build:

```powershell
./scripts/build.ps1
```

Testes:

```powershell
./scripts/test.ps1
```

Cobertura:

```powershell
./scripts/coverage.ps1
```

## 10. Parar ou resetar a stack

Parar containers preservando volumes:

```powershell
./scripts/compose-down.ps1
```

Parar e apagar dados locais:

```powershell
./scripts/compose-down.ps1 -Volumes
```

Equivalente Docker Compose:

```powershell
docker compose down
docker compose down -v
```

## 11. Executar API fora do container

Se quiser depurar a API localmente, suba apenas o PostgreSQL:

```powershell
docker compose up -d postgres
```

Depois execute:

```powershell
dotnet run --project src/BalanceControl.Api/BalanceControl.Api.csproj
```

As migrations sao aplicadas no startup quando `DatabaseSettings__RunMigrationsOnStartup=true`.
