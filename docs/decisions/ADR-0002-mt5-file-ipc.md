# ADR-0002: Atomic file IPC for MT5

- Status: Accepted
- Date: 2026-08-21

## Decision

Use a versioned atomic command/acknowledgement queue in MT5 Common Files, with expiry, sequencing and idempotency. The EA remains the final account and execution safety gate.

## Consequences

This avoids third-party DLLs and favors reliability over HFT latency. Queue recovery, cleanup, access permissions and clock/freshness behavior require simulator and failure-injection tests. Any replacement requires a superseding ADR and equivalent safety proof.
