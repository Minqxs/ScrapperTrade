# Strategy Validation Loop

- Trigger: new/changed versioned strategy specification. Objective: determine robustness, never promise profit.
- Agents: Quant, QA, Reviewer/Skeptic, Product.
- States: SCHEMA -> SEMANTIC/RISK COMPATIBILITY -> BASELINE -> TRAIN/VALIDATION -> OUT-OF-SAMPLE -> WALK-FORWARD -> SENSITIVITY -> COST/STRESS -> MONTE CARLO -> VERDICT -> SHADOW ELIGIBILITY.
- Gates: adequate sample, conservative intrabar handling, spread/slippage/commission, drawdown and stability limits, reproducible data/config/version.
- Retry: rejection creates a new candidate/version; no tuning on held-out results. Three unstable iterations trigger hypothesis review.
- Stop/outputs: reject or promote only to next governed status; retain immutable inputs, metrics, lineage and verdict.
- Boundaries/budget: bounded searches and resource caps; production activation and risk-policy changes require user-governed gates.
