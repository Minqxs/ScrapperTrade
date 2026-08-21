# Strategy Research and Governance

Strategy research is evidence production, not execution authority. The persisted lifecycle deliberately separates immutable specifications, validation evidence, shadow comparison, and explicit user activation.

## Evidence model

- Strategy definitions own sequential, immutable version records. Each specification is stored with a SHA-256 hash.
- Backtest runs retain dataset references, cost models, in-sample/out-of-sample identity, metrics, and individual trades as distinct evidence.
- Validation runs retain their validation kind, status, and structured evidence without claiming profitability.
- Research candidates require provenance. Ambiguous candidates cannot receive validation approval until their ambiguity state is resolved.
- Lineage records link derived versions to parents and the candidate that motivated the change.

## Authority boundary

`ResearchGovernanceRepository` can record candidates and passed validation evidence. Its public surface has no activation, promotion, retirement, permission, risk-policy, or execution operation. Validation changes a version only from `DRAFT` to `VALIDATED`.

`UserStrategyGovernanceRepository` owns activation, challenger promotion, and retirement. These transitions require:

- an explicit user identity;
- a non-empty, uniquely consumed confirmation ID;
- a reason retained in append-only governance audit;
- a validated version;
- a completed matching shadow comparison when one is supplied for challenger promotion.

At most one activation per strategy definition may remain open; SQLite enforces this with a filtered unique index. Promotion closes the old activation and marks its version superseded in the same transaction. Research approval never changes instrument permissions, hard risk policy, system state, or execution configuration.

## Shadow comparison

Only an active champion and validated challenger can enter a shadow comparison. Completing comparison stores champion metrics, challenger metrics, and decision evidence, but does not promote anything. Promotion remains a separate user-confirmed transaction.
