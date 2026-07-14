# Persistence

PostgreSQL is the source of truth.

## Tables

### `balance_control.tb_user_balance`

Stores the current materialized balance per user.

| Column | Purpose |
|---|---|
| `user_id` | User identifier and primary key |
| `balance` | Current balance |
| `version` | Number of applied balance changes |
| `created_at` | First balance creation timestamp |
| `updated_at` | Last balance update timestamp |

### `balance_control.tb_balance_movement`

Stores the append-only movement ledger.

| Column | Purpose |
|---|---|
| `id` | Movement identifier |
| `user_id` | User identifier |
| `operation_id` | Idempotency/event identifier supplied by the client |
| `amount` | Delta applied to the balance |
| `balance_after` | Balance after the movement |
| `request_hash` | Canonical payload hash for conflict detection |
| `occurred_at` | Business occurrence timestamp |
| `created_at` | Processing timestamp |
| `description` | Optional description |

## Indexes and constraints

- Primary key on `tb_user_balance.user_id`.
- Unique index on `(user_id, operation_id)` in `tb_balance_movement`.
- Index on `(user_id, created_at, id)` for statement reads.

## Why materialized balance plus ledger

Computing the balance by summing all movements on every read becomes expensive as volume grows. The current balance table gives constant-time balance reads, while the ledger preserves traceability and statement queries.

## Volume evolution

For larger volumes, the next persistence changes would be:

- Partition `tb_balance_movement` by time.
- Keep statement queries paged or cursor-based.
- Archive old movements according to retention requirements.
- Add read replicas for statement-heavy workloads.
