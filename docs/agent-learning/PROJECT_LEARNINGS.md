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

### YYYY-MM-DD — Topic

- Context/evidence:
- Learning/decision:
- Failed approach (if any):
- Affected files or future action:
- Supersedes:
