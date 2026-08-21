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

### 2026-08-21 — Offline knowledge files and SQLite FTS

- Context/evidence: Migration-backed tests ingest, deduplicate, search, cite, soft-delete, restore, expire, and purge local UTF-8 documents without a provider or network dependency.
- Learning/decision: Private inputs use SHA-256 content-addressed paths outside Git; original filenames are metadata only. SQLite FTS5 is maintained by migration-owned triggers, while chunks retain character offsets for inspectable provenance.
- Failed approach: On Windows, keeping the hash input stream alive until method exit prevented the staging file's atomic move. The stream must close before `File.Move`.
- Affected files or future action: `Infrastructure/Knowledge`, `OfflineKnowledgeFoundation` migration, and knowledge ingestion architecture documentation.

- Supersedes: none.

### 2026-08-21 — Research evidence is not activation authority

- Context/evidence: Migration-backed governance tests validate candidate provenance, ambiguity blocking, research-only validation, explicit user confirmation, shadow comparison, promotion, and retirement.
- Learning/decision: `DRAFT -> VALIDATED` belongs to research evidence; `VALIDATED -> ACTIVE`, promotion, and retirement belong to a separate user governance surface and append-only audit. A filtered unique SQLite index enforces one open activation per strategy definition.
- Failed approach: none.
- Affected files or future action: `StrategyGovernance` persistence and architecture documentation; preserve this split when APIs and background research orchestration are added.
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

### 2026-08-21 - Shadow scheduler authority boundary

- Context/evidence: Deterministic checks cover user-only enablement, paused-system rejection, risk approval, duplicate evaluation, and state reload after restart.
- Learning/decision: The initial scheduler ends at a persisted shadow decision and has no execution adapter or MT5 command queue. Idempotency keys bind strategy version, instrument, and market observation time.
- Failed approach: Relying only on portfolio risk for freshness allowed stale no-signal evaluation. The scheduler now validates freshness and strict candle ordering first.
- Affected files or future action: `SimulatorStrategyRuntime.cs`, `JsonShadowStrategyStateStore.cs`; future execution promotion must use a separate user-gated boundary.
- Supersedes: none.

### 2026-08-21 - Backtest time and cost semantics

- Context/evidence: Deterministic checks verify next-bar entry, adverse cost impact, chronological embargoes, non-overlapping walk-forward folds, and fail-closed sample thresholds.
- Learning/decision: Close-derived signals may first enter at the next bar open. Spread, entry/exit slippage, and commission are separated from gross R; stop/target ambiguity resolves adversely and stop gaps fill at the worse opening price.
- Failed approach: Same-close execution would use a price that was unavailable until the signal bar completed and therefore introduced lookahead bias.
- Affected files or future action: `StrategyValidationEngine.cs`; preserve these semantics when runtime/backtest parity is expanded to additional strategy types.
- Supersedes: the bootstrap backtester remains a minimal compatibility check, while this engine is the costed validation path.
