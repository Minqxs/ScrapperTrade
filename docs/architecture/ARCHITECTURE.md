# Architecture

## Authority and execution

`User -> Permissions -> Hard Risk Policy -> Portfolio Risk Engine -> Strategy Engine -> Execution Safety Gate -> MT5 EA -> Broker`

Market data is normalized into deterministic market state and regime evidence. Strategies emit attributed candidate trades, never orders. Portfolio risk independently approves/rejects. The execution gate produces idempotent commands. The thin EA revalidates freshness, account, symbol and emergency state before broker interaction and reports broker truth back for reconciliation.

## Bounded contexts

- Control & Permissions: system state, universe, user controls, immutable audit intent.
- Market Intelligence: broker-neutral metadata, candles, indicators, sessions and regimes.
- Strategy & Validation: versioned constrained specifications, runtime evaluation, backtests and robustness.
- Portfolio Risk: sizing, loss/exposure/concentration limits and approval evidence.
- Execution: command lifecycle, reconciliation, MT5 protocol, hedging/netting semantics.
- Knowledge & Research: sources, transcripts, provenance, hypotheses and challengers.
- Operations: configuration, health, persistence, recovery, logs and local lifecycle.

Domain/Application/Quant logic must not depend on ASP.NET, SQLite, MQL5, UI, or an AI provider. Infrastructure implements ports. The Host composes them and serves the React application over loopback. Runtime data lives in a per-user application-data directory, not the repository.

## Hot path versus research path

Hot path: `Market Data -> Market State -> Regime -> Strategy -> Candidate -> Risk -> Execution Gate -> MT5`. It is deterministic, bounded, observable, and available without an LLM.

Research path ingests content and trade evidence, proposes schema-constrained candidates, and invokes offline validation. Its only bridge back is a governed strategy version/status transition; it has no execution port.

## Reliability model

SQLite is the initial durable store. Commands use an atomic, versioned file queue in MT5 Common Files with unique IDs, sequence/timestamps, acknowledgements, stale rejection and idempotency. State is reconciled against broker truth after restart. Unknown/stale health blocks entries while risk-reducing management remains possible under explicit policy.

## Deployment evolution

Initial deployment is one Windows PC: browser -> loopback ASP.NET host/background services -> SQLite and MT5 file IPC -> EA. Future Windows VPS/platform separation changes adapters and deployment topology, not domain, risk, strategy, or validation semantics.
