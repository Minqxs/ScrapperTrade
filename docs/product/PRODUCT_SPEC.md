# ScrapperTrade Product Specification

## Product promise

ScrapperTrade is a local-first Windows control centre for safely developing, evaluating, and DEMO-forward-testing algorithmic day-trading hypotheses through MetaTrader 5. It helps a user discover whether an edge exists; it does not guarantee profit.

## Primary journeys

1. Install and diagnose locally, start the host, and complete a first-run wizard without paid infrastructure.
2. Connect an MT5 DEMO terminal, inspect account/mode/health, discover symbols, and explicitly map/allow a trading universe.
3. Simulate or manually submit a risk-checked DEMO order, observe its complete audit trail, and safely close it.
4. Pause all new entries, one instrument, or one strategy while protective management continues; close all; emergency-lock and require user reactivation.
5. Create/version a constrained strategy, backtest with realistic costs, validate out of sample, shadow it, and govern promotion/retirement.
6. Ingest local documents/media, retain provenance, use manual/local/optional API AI providers to form candidate specifications, and keep AI outside execution.
7. Recover after restart without duplicate/stale commands, inspect health and evidence, and operate from clear documentation.

## Safety acceptance

- Automated trading fails closed unless broker truth is positively DEMO and healthy/fresh.
- User permissions can narrow but AI cannot expand the allowed universe, alter hard risk policy, unlock emergencies, or promote itself.
- Strategy outputs are candidates; independent portfolio risk and final EA gates decide eligibility.
- Close-all first blocks entries, cancels pending orders, closes and verifies positions, then remains paused.
- Every rejection, modification, order, and close is attributable and durable.

## Functional scope

The required scope is represented by Buckets 0–14 in `.agentic/build-state/build-state.json`: local application, MT5 bridge, execution controls, portfolio risk, market intelligence, strategies/backtests, autonomous DEMO operation, knowledge/AI research, champion/challenger governance, recovery/security, finished UX, and clean-checkout release validation.

## Explicit non-goals for initial release

Real-money enablement, remote/VPS deployment, HFT latency, dynamically compiled AI code, guaranteed profitability, a mandatory paid AI provider, and broker-specific lock-in.

## Cross-cutting acceptance

Observable system states are STOPPED, STARTING, RUNNING, PAUSED, MAINTENANCE, DEGRADED, and EMERGENCY_LOCKED; uncertainty prevents entries. Responsive UI, simulator lifecycle, deterministic financial tests, actual (not asserted) MQL5 compilation where available, secrets hygiene, and actionable local setup documentation are release gates.
