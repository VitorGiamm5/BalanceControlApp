# Observability

The local stack includes logs and metrics capture with visual inspection tools.

## Endpoints

| Tool | URL | Purpose |
|---|---|---|
| API metrics | `http://localhost:9005/metrics` | raw Prometheus metrics |
| Seq | `http://localhost:8081` | structured log capture and search, without local authentication |
| Prometheus | `http://localhost:9090` | metrics capture and query |
| Grafana | `http://localhost:3000` | metrics dashboards |

Grafana local credentials:

```text
admin / admin
```

## Logs

The API writes structured JSON logs to stdout and, when `SEQ_URL` is configured, sends the same application logs to Seq.

In Docker Compose:

```text
SEQ_URL=http://seq:5341
```

Useful Seq searches:

```text
RequestPath like '/api/v1/balances%'
StatusCode >= 400
@Level = 'Error'
```

## Requests by period

Use Seq when you need to inspect all HTTP requests in a time window.

UI flow:

1. Open `http://localhost:8081`.
2. Use the time range picker in the top-right corner.
3. Select the desired period, for example last 15 minutes or a custom interval.
4. Search for:

```text
SourceContext = 'Serilog.AspNetCore.RequestLoggingMiddleware'
```

Balance endpoints only:

```text
SourceContext = 'Serilog.AspNetCore.RequestLoggingMiddleware'
and RequestPath like '/api/v1/balances%'
```

Only failed requests:

```text
SourceContext = 'Serilog.AspNetCore.RequestLoggingMiddleware'
and StatusCode >= 400
```

Via Seq API, for export or evidence:

```powershell
$from = [Uri]::EscapeDataString("2026-07-13T18:00:00Z")
$to = [Uri]::EscapeDataString("2026-07-13T19:00:00Z")
$filter = [Uri]::EscapeDataString("SourceContext = 'Serilog.AspNetCore.RequestLoggingMiddleware'")
Invoke-WebRequest -UseBasicParsing "http://localhost:8081/api/events?from=$from&to=$to&filter=$filter&count=1000" |
    Select-Object -ExpandProperty Content
```

The request log events include useful fields such as `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`, `TraceId`, `SpanId`, service version and environment.

## Metrics

Prometheus scrapes the API every 5 seconds from:

```text
api:9005/metrics
```

The API exposes runtime metrics and HTTP metrics through `prometheus-net`.

The provisioned Grafana dashboard is:

```text
Balance Control / Balance Control Overview
```

It includes HTTP throughput, latency, CPU and memory panels.

## Operational checks

After starting the stack:

```powershell
./scripts/compose-up.ps1 -Build
./scripts/smoke.ps1
```

Then inspect:

```text
Swagger:    http://localhost:9005/swagger
Seq:        http://localhost:8081
Prometheus: http://localhost:9090/targets
Grafana:    http://localhost:3000
```
