# How to use

Este guia mostra uma sequencia manual simples para testar a API pelo Swagger ou por qualquer cliente HTTP.

Antes de comecar, suba a stack conforme `docs/how-to-run.md` e abra:

```text
http://localhost:9005/swagger
```

## 1. Gerar token JWT

Endpoint:

```http
POST /api/v1/auth/token
```

Payload:

```json
{
  "clientId": "balance-client",
  "clientSecret": "balance-secret"
}
```

Copie o campo:

```text
data.accessToken
```

No Swagger, clique em `Authorize` e informe:

```text
Bearer <accessToken>
```

## 2. Adicionar saldo

Endpoint:

```http
POST /api/v1/balances/adjustments
```

Payload valido:

```json
{
  "userId": "user-0001",
  "operationId": "11111111-1111-4111-8111-111111111111",
  "amount": 100.50,
  "occurredAt": "2026-07-13T18:00:00Z",
  "description": "manual test credit"
}
```

Resultado esperado:

```text
200 OK
applied = true
balance = 100.50
```

## 3. Testar replay idempotente

Envie exatamente o mesmo payload de credito novamente:

```json
{
  "userId": "user-0001",
  "operationId": "11111111-1111-4111-8111-111111111111",
  "amount": 100.50,
  "occurredAt": "2026-07-13T18:00:00Z",
  "description": "manual test credit"
}
```

Resultado esperado:

```text
200 OK
applied = false
balance = 100.50
```

O saldo nao deve ser aplicado duas vezes.

## 4. Tirar saldo

Use outro `operationId`, mantendo o mesmo `userId`:

```json
{
  "userId": "user-0001",
  "operationId": "22222222-2222-4222-8222-222222222222",
  "amount": -25.00,
  "occurredAt": "2026-07-13T18:05:00Z",
  "description": "manual test debit"
}
```

Resultado esperado:

```text
200 OK
applied = true
balance = 75.50
```

## 5. Consultar saldo

Endpoint:

```http
GET /api/v1/balances/user-0001
```

Resultado esperado:

```text
200 OK
balance = 75.50
```

## 6. Consultar extrato

Endpoint:

```http
GET /api/v1/balances/user-0001/statement?page=1&pageSize=50
```

Resultado esperado:

```text
200 OK
totalItems = 2
```

O extrato deve conter duas movimentacoes aplicadas:

- credito de `100.50`;
- debito de `-25.00`.

O replay do passo 3 nao deve criar uma nova movimentacao.

## 7. Testar conflito idempotente

Envie o mesmo `userId` e `operationId` do primeiro credito, mas altere algum campo do payload:

```json
{
  "userId": "user-0001",
  "operationId": "11111111-1111-4111-8111-111111111111",
  "amount": 101.00,
  "occurredAt": "2026-07-13T18:00:00Z",
  "description": "changed manual test credit"
}
```

Resultado esperado:

```text
409 Conflict
```

Isso prova que a API diferencia replay legitimo de reutilizacao incorreta da mesma chave idempotente.

Validacao automatizada equivalente:

```powershell
./scripts/idempotency.ps1
```

Esse script executa o mesmo roteiro que um avaliador faria manualmente: primeira aplicacao, replay exato, conflito por payload diferente, consulta de saldo e consulta de extrato.

## 8. Testar validacao

Exemplo invalido:

```json
{
  "userId": "user-0001",
  "operationId": "33333333-3333-4333-8333-333333333333",
  "amount": 0,
  "occurredAt": "2026-07-13T18:10:00Z",
  "description": "invalid zero amount"
}
```

Resultado esperado:

```text
422 Unprocessable Entity
```

O contrato completo dos campos esta em `docs/api-payloads.md`.

## 9. Ver requisicoes por periodo

Abra o Seq:

```text
http://localhost:8081
```

Use o seletor de periodo no canto superior direito e pesquise:

```text
SourceContext = 'Serilog.AspNetCore.RequestLoggingMiddleware'
```

Para ver apenas chamadas da API de saldo:

```text
SourceContext = 'Serilog.AspNetCore.RequestLoggingMiddleware'
and RequestPath like '/api/v1/balances%'
```

Para ver apenas falhas:

```text
SourceContext = 'Serilog.AspNetCore.RequestLoggingMiddleware'
and StatusCode >= 400
```

Os eventos exibem metodo, rota, status HTTP, tempo de resposta, `TraceId` e metadados do servico.
