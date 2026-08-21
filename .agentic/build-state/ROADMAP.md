# Delivery Roadmap

The 15 required buckets are preserved in `build-state.json`. The critical path is foundation -> MT5/safety -> portfolio intelligence -> strategy/autonomous demo -> governance/hardening -> release. Knowledge ingestion may proceed after the local foundation and joins strategy research at Bucket 9.

Bucket exits are acceptance gates, not dates. Each exit requires the bucket integration loop. Bucket 0 must establish solution/UI/docs/scripts/CI/storage/simulator foundations before Bucket 1 is marked ready. Actual implementation tasks are decomposed only when their dependency contracts are known; this prevents speculative “complete” state.

Highest-risk proofs are front-loaded: DEMO-account fail-closed behavior, IPC idempotency/recovery, broker-metadata position sizing, portfolio loss/exposure limits, shared runtime/backtest semantics, and emergency-lock authority. Simulator evidence is required before broker smoke testing.
