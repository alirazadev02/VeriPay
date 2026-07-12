# VeriPay — Interbank Transfer Orchestration & Tracking Backend

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet)
[![EF Core](https://img.shields.io/badge/EF_Core-8.0-512BD4)](https://learn.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/Microsoft_SQL_Server-2019%2B-CC2927?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> Final Year Project — BS Computer Science, Government College University Faisalabad
> Mobile client: [InterbankTransferApp](https://github.com/alirazadev02/InterbankTransferApp)

VeriPay is a real-time interbank transfer orchestration and tracking system built to close the visibility gap in Pakistan's payment rails. When a transfer is dispatched over **1LINK** or **RAAST**, it passes through multiple independent institutions — the initiating bank, the national switch, and the beneficiary bank — and today, none of that journey is visible to the customer until it succeeds, delays, or fails. VeriPay makes every hop of that journey a queryable, real-time event.

This repository contains the two backend microservices and the shared data layer. The React Native mobile client lives in [`InterbankTransferApp`](https://github.com/alirazadev02/InterbankTransferApp).

## Architecture

```
React Native App  ──▶  Orchestrator (:7002)  ──▶  Registry (:7001)  ──▶  MS SQL Server
                         [write side / CQRS]        [read side / CQRS]
```

- **VeriPay.Orchestrator** (port 7002) — the command side. Accepts transfer/reversal requests, persists the transfer + initial event, updates wallet balances, launches the background simulation engine, and fires webhooks on every status change.
- **VeriPay.Registry** (port 7001) — the query side. Pure read model serving timelines, transaction history, wallet balances, and failed-transfer lookups to the mobile app's 3-second polling loop.
- **VeriPay.Shared** — shared EF Core entity models (`Transfer`, `TransferEvent`, `Wallet`, `Bank`) and `VeriPayDbContext`, referenced by both services to avoid duplicated data-access code.

This CQRS-inspired split means high-frequency read polling from the mobile client never contends with the write-heavy transfer pipeline.

## Key Features

- **Real-time transfer tracking** — every status transition is stored as an immutable, timestamped event and surfaced to the client via 3-second polling.
- **Dual rail support** — 1LINK and RAAST, with rail-specific event metadata.
- **Self-service reversal workflow** — failed transfers can be reversed end-to-end, crediting the wallet back atomically.
- **Idempotent submission** — an `Idempotency-Key` header, checked via an `IAsyncActionFilter`, guarantees network retries never create duplicate transfers.
- **Webhook notifications** — asynchronous HTTP POST callbacks to any registered subscriber on every status event, with a fire-log for delivery auditing.
- **Demo scenario engine** — four pre-configured simulation paths (Happy Path, Delay at Switch, Force Fail, Auto-Reversal) for reliable, repeatable demos.
- **Append-only audit trail** — every event carries a `source` (Client / switch / beneficiary bank), a millisecond-precision timestamp, and a JSON details payload, giving a tamper-evident, fully reconstructable history for every transfer.

## Transfer State Machine

| Status | Label | Terminal? |
|---|---|---|
| 1 | Initiated | No |
| 2 | Sent to Switch | No |
| 3 | At Beneficiary | No |
| 4 | Credited | ✅ |
| 5 | Failed | ✅ |
| 6 | Reversal Pending | No |
| 7 | Reversed | ✅ |

```
1 Initiated → 2 SentToSwitch → 3 AtBeneficiary → 4 Credited              (happy path)
1 Initiated → 2 SentToSwitch → 5 Failed → 6 RevPending → 7 Reversed      (failure + reversal)
```

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 (REST API, two independently deployable microservices) |
| ORM | Entity Framework Core 8 |
| Database | Microsoft SQL Server 2019+ |
| Notifications | Webhook HTTP POST (async, fire-and-forget) |
| Concurrency | `IServiceScopeFactory`-scoped `DbContext` per background write, `ConcurrentDictionary`-backed idempotency store and webhook registry |

## Project Structure

```
VeriPay/
├── VeriPay.Orchestrator/   # Write-side API (:7002) — transfers, reversals, webhooks, simulation engine
├── VeriPay.Registry/       # Read-side API (:7001)   — timelines, history, wallet balances
├── VeriPay.Shared/         # EF Core entities + DbContext shared by both services
└── veripay_schema.sql      # Idempotent schema (IF NOT EXISTS guards) — safe to re-run
```

## Getting Started

**Prerequisites:** .NET 8 SDK, Microsoft SQL Server 2019+, SSMS or Azure Data Studio.

1. Run the schema against your SQL Server instance:
   ```bash
   sqlcmd -S <server> -i veripay_schema.sql
   ```
2. Update the connection string in `VeriPay.Orchestrator/appsettings.json` and `VeriPay.Registry/appsettings.json` if your instance name differs from the default.
3. Start both services (or use the compound launch config in `.vscode/launch.json` to run both at once):
   ```bash
   dotnet run --project VeriPay.Orchestrator   # http://localhost:7002
   dotnet run --project VeriPay.Registry        # http://localhost:7001
   ```
4. Point the [mobile app](https://github.com/alirazadev02/InterbankTransferApp)'s Settings → Server IP at this machine's LAN IP.

## API Reference

| Endpoint | Method | Service | Purpose |
|---|---|---|---|
| `/transfers/track` | POST | Orchestrator | Submit a transfer or reversal (idempotent) |
| `/transfers/health` | GET | Orchestrator | Health check |
| `/webhooks` | GET / POST | Orchestrator | List / register webhook subscriptions |
| `/webhooks/{id}` | DELETE | Orchestrator | Remove a subscription |
| `/webhooks/logs` | GET | Orchestrator | Last N delivery attempts |
| `/registry/transfers/{id}/timeline` | GET | Registry | Full event history for a transfer |
| `/registry/transfers` | GET | Registry | Most recent transfers |
| `/registry/transfers/history` | GET | Registry | Paginated history by client |
| `/registry/transfers/failed` | GET | Registry | Failed transfers eligible for reversal |
| `/registry/wallet/{clientId}` | GET | Registry | Current wallet balance |
| `/registry/health` | GET | Registry | Health check |

## Security & Data Integrity

- Idempotency enforced at both the application layer (filter) and database layer (`transfer_id` uniqueness constraint).
- `CHECK` constraints on `rail`, `type`, and `current_status` columns prevent invalid data regardless of entry point.
- Designed for HTTPS/TLS, JWT auth, and Redis-backed distributed state in production — see the full documentation for the production hardening roadmap.

## Documentation

The full 70-page FYP report — covering requirements, system design, class/sequence/state diagrams, ERD, testing (18 integration test cases, all passing), and complete frontend screen-by-screen documentation — is maintained separately as part of the academic submission.

---

**Author:** Ali Raza — 2022-GCUF-055559
**Supervisor:** Dr. Shahla Gul
**Department:** Computer Science, Government College University Faisalabad
