# ADR-0003: Constrained strategy specifications, no generated executable code

- Status: Accepted
- Date: 2026-08-21

## Decision

Runtime and backtesting interpret the same versioned, schema-validated strategy specification. AI output is untrusted candidate data; arbitrary generated C#, MQL5, scripting, or evaluation is prohibited.

## Consequences

Semantics are auditable and reproducible, and research cannot gain execution authority. The DSL evolves through explicit schema versions/migrations and compatibility tests; expressiveness is intentionally bounded.
