# Authentication

The API uses a simple JWT flow for the exercise environment.

## Token endpoint

```http
POST /api/v1/auth/token
Content-Type: application/json

{
  "clientId": "balance-client",
  "clientSecret": "balance-secret"
}
```

Successful response:

```json
{
  "data": {
    "accessToken": "...",
    "tokenType": "Bearer",
    "expiresAt": "2026-07-13T22:00:00Z"
  },
  "errors": []
}
```

Use the token in protected balance endpoints:

```http
Authorization: Bearer <accessToken>
```

## Local credentials

The local Docker stack sets:

| Setting | Value |
|---|---|
| `Jwt__ClientId` | `balance-client` |
| `Jwt__ClientSecret` | `balance-secret` |
| `Jwt__Issuer` | `BalanceControl` |
| `Jwt__Audience` | `BalanceControl.Api` |

This is intentionally simple authentication for the technical exercise. In a production service, these values should come from a secret manager and the token issuer should be externalized.
