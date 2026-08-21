# Simulator Strategy Runtime

The strategy scheduler is deliberately shadow-only. It accepts a validated `StrategySpec`, deterministic market input, user-owned enablement, explicit system state, instrument permissions, and the hard portfolio-risk engine. Its only output is a persisted `ShadowStrategyDecision`.

The scheduler has no execution adapter, MT5 queue, or broker-command dependency. A risk-approved outcome records simulated volume and risk; it is not an order authorization.

Evaluation requires `Shadow` lifecycle, user enablement, `RUNNING` state, an allowed instrument, ordered fresh data, eligible regime, a deterministic signal, and risk approval. The atomic JSON state store makes repeated and post-restart evaluation idempotent.
