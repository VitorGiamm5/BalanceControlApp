# API payloads

Esta pagina documenta o contrato dos payloads usados no Swagger, scripts smoke e testes de avaliacao.

## Autenticacao

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

| Campo | Tipo | Obrigatorio | Validacao | Observacao |
|---|---|---:|---|---|
| `clientId` | string | Sim | Deve bater com `Jwt__ClientId` | No ambiente local: `balance-client` |
| `clientSecret` | string | Sim | Deve bater com `Jwt__ClientSecret` | No ambiente local: `balance-secret` |

Credenciais invalidas retornam `401 Unauthorized`. Payload ausente ou JSON invalido retorna `400 Bad Request`.

## Alteracao de saldo

Endpoint:

```http
POST /api/v1/balances/adjustments
Authorization: Bearer <accessToken>
```

Payload exemplo com `userId` estatico:

```json
{
  "userId": "user-0001",
  "operationId": "11111111-1111-4111-8111-111111111111",
  "amount": 100.50,
  "occurredAt": "2026-07-13T18:00:00Z",
  "description": "initial credit"
}
```

| Campo | Tipo | Obrigatorio | Validacao | Observacao |
|---|---|---:|---|---|
| `userId` | string | Sim | Nao pode ser vazio; maximo 100 caracteres; espacos laterais sao removidos | Usuario inexistente e criado na primeira alteracao |
| `operationId` | UUID | Sim | Nao pode ser `00000000-0000-0000-0000-000000000000` | Chave idempotente por usuario |
| `amount` | decimal | Sim | Diferente de zero; positivo ou negativo; maximo 2 casas decimais; deve caber em `numeric(18,2)` | Valor positivo credita, valor negativo debita |
| `occurredAt` | date-time | Nao | Deve ser um `date-time` JSON valido, preferencialmente UTC com sufixo `Z` | Se omitido, a API usa o horario do processamento |
| `description` | string | Nao | Maximo 500 caracteres; espacos laterais sao removidos; string vazia vira `null` | Texto livre para identificar o evento |

Respostas principais:

| Cenario | Status | Resultado |
|---|---:|---|
| Nova operacao valida | `200 OK` | Aplica o saldo e retorna `applied = true` |
| Replay com mesmo `userId`, `operationId` e mesmo payload | `200 OK` | Nao reaplica saldo e retorna `applied = false` |
| Mesmo `userId` e `operationId` com payload diferente | `409 Conflict` | Indica conflito idempotente |
| Payload invalido por regra de negocio | `422 Unprocessable Entity` | Retorna detalhes de validacao |
| JSON invalido | `400 Bad Request` | Retorna erro de parse do payload |
| Token ausente ou invalido | `401 Unauthorized` | Requer novo token Bearer valido |

## Consulta de saldo

Endpoint:

```http
GET /api/v1/balances/user-0001
Authorization: Bearer <accessToken>
```

| Parametro | Tipo | Obrigatorio | Validacao | Observacao |
|---|---|---:|---|---|
| `userId` | string path | Sim | Espacos laterais sao removidos no service | Usuario sem saldo registrado retorna `404 Not Found` |

## Consulta de extrato

Endpoint:

```http
GET /api/v1/balances/user-0001/statement?page=1&pageSize=50
Authorization: Bearer <accessToken>
```

| Parametro | Tipo | Obrigatorio | Validacao | Observacao |
|---|---|---:|---|---|
| `userId` | string path | Sim | Espacos laterais sao removidos no service | Usuario sem saldo registrado retorna `404 Not Found` |
| `page` | integer query | Nao | Valores menores ou iguais a zero viram `1` | Pagina inicial |
| `pageSize` | integer query | Nao | Valores menores ou iguais a zero viram `50`; valores acima de `200` sao limitados a `200` | Tamanho da pagina |

## Cobertura atual das validacoes

As validacoes de `AdjustBalanceRequest` cobrem:

- `userId` ausente, em branco e acima de 100 caracteres.
- `operationId` vazio.
- `amount` zero, fora da precisao `numeric(18,2)` e com mais de 2 casas decimais.
- `amount` positivo e negativo como casos validos.
- `description` acima de 500 caracteres.

As consultas nao criam usuarios automaticamente; para `userId` inexistente, saldo e extrato retornam `404 Not Found`.
