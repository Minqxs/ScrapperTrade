# Project Learnings

Read before implementation. Add dated, evidence-backed entries; identify superseded guidance rather than deleting history.

## 2026-08-21 — Repository discovery

- The repository contained only `.git` when governance work began; no source, scripts, CI, or prior guidance existed.
- The supplied specification is the initial source of truth. Build-state intentionally marks only governance/architecture as in progress and records no test, review, merge, MT5, or compile success.

## Validated design constraints

- Safety authority must be structural across ports and tests, not merely configuration or UI wording.
- Simulator/EA protocol parity and broker reconciliation are prerequisites for meaningful MT5 verification.
- Runtime/backtest semantic sharing is essential to prevent research/runtime drift.
- Human-only dependencies should be represented as narrow evidence gates so the rest of delivery remains resumable.

### 2026-08-21 — SQLite event ordering

- Context/evidence: EF Core SQLite 8 rejects server-side `ORDER BY` over `DateTimeOffset` values.
- Learning/decision: Append-only audit and system-event streams use monotonic SQLite integer IDs for deterministic newest-first reads; timestamps remain evidence fields rather than ordering keys.
- Failed approach: Ordering by `OccurredAt` threw `NotSupportedException` in the migration-backed integration test.
- Affected files or future action: `Persistence/Repositories.cs`; retain monotonic IDs when extending event storage.
- Supersedes: none.

## Entry template

### 2026-08-21 — Portfolio exposure and session boundaries

- Context/evidence: Deterministic risk tests cover disabled instruments, side permissions, UTC and overnight sessions, duplicate signals, rolling order frequency, and same-direction exposure groups.
- Learning/decision: Group risk remains the aggregate cap, while a separate same-direction cap models correlated concentration; equality with either cap is allowed and only excess is rejected. Sessions are UTC half-open intervals and overnight sessions retain the configured start day.
- Failed approach: A position reconciliation query ordered by `DateTimeOffset`, repeating SQLite's unsupported translation; stable logical/broker identifiers now provide deterministic ordering.
- Affected files or future action: `TradingDomain.cs`, `TradingServices.cs`, and persistence repositories/tests.
- Supersedes: none.

### 2026-08-21 — MT5 clock domains and local process ownership

- Context/evidence: A live Common Files heartbeat carried `TimeTradeServer()` as Unix time, which was several hours ahead of the host UTC clock. The fail-closed reader correctly rejected it as future-dated.
- Learning/decision: All IPC timestamps and expiry comparisons use `TimeGMT()`/UTC. Broker-server time is market metadata, never protocol wall-clock time.
- Failed approach: Treating MT5 `datetime` from `TimeTradeServer()` as UTC Unix seconds.
- Affected files or future action: `ScrapperTradeEA.mq5`; retain live host/EA clock-skew tests.

- Context/evidence: Starting Vite through `npm.cmd` recorded the wrapper PID, leaving its Node child alive after stop and allowing a stale SPA to impersonate the new stack.
- Learning/decision: Launch the Vite Node entry point directly, record the real PID, and fail startup when loopback ports are occupied.
- Affected files or future action: `scripts/start.ps1`; add crash/restart E2E coverage during operational hardening.

### YYYY-MM-DD — Topic

- Context/evidence:
- Learning/decision:
- Failed approach (if any):
- Affected files or future action:
- Supersedes:
