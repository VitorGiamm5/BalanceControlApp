# Concurrency And Idempotency

The critical requirement is that repeated and concurrent requests do not corrupt balances or duplicate effects.

## Operation identity

Each balance adjustment carries:

- `userId`
- `operationId`
- `amount`
- optional `description`

The durable idempotency key is `(userId, operationId)`.

## Replay behavior

| Scenario | Result |
|---|---|
| New `(userId, operationId)` | Applies balance change and creates a movement |
| Same `(userId, operationId)` and same payload | Returns the previous result with `applied = false` |
| Same `(userId, operationId)` and different payload | Returns `409 Conflict` |

## How to validate idempotency

The validation endpoint is the real balance adjustment endpoint:

```http
POST /api/v1/balances/adjustments
```

Use this sequence:

1. Send a new adjustment with a new `operationId`.
2. Send the exact same payload again.
3. Send the same `userId` and `operationId` with a different payload.
4. Query balance and statement.

Expected results:

| Step | Expected result |
|---|---|
| First adjustment | `200 OK`, `applied = true` |
| Exact replay | `200 OK`, `applied = false`, same `movementId` |
| Same key with different payload | `409 Conflict` |
| Balance query | Balance reflects only one applied movement |
| Statement query | Statement contains only one movement for the replayed operation |

Automated local validation:

```powershell
./scripts/idempotency.ps1
```

## How consistency is maintained

Balance updates are executed in one database transaction.

The current balance is updated with PostgreSQL:

```sql
insert into balance_control.tb_user_balance (...)
values (...)
on conflict (user_id)
do update set
    balance = balance + excluded.balance,
    version = version + 1,
    updated_at = excluded.updated_at
returning balance;
```

This makes PostgreSQL serialize concurrent updates to the same `user_id` row while allowing different users to be processed in parallel.

The movement insert is protected by a unique index on `(user_id, operation_id)`. If two API instances race on the same replayed operation, one insert wins and the other reads the stored movement.

## Why not cache-based idempotency

Cache-based idempotency can lose correctness if the API writes the balance and crashes before caching the response. This implementation stores the idempotency decision with the durable ledger, so replays remain safe across process restarts and multiple API instances.
