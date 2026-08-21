# Feature Delivery Loop

- Trigger: ready task with satisfied dependencies.
- Objective/scope: deliver one coherent vertical capability; exclude unrelated refactors.
- Agents: Planner, Architecture/UI as relevant, implementation owner, QA, Reviewer, Git/PR.
- Tools/permissions: repository and local test tools; network/external mutation only when authorized.
- States: PLAN -> UX/ARCHITECTURE -> IMPLEMENT -> UNIT/INTEGRATION TEST -> RUN FEATURE -> VISUAL QA (if UI) -> REVIEW -> REPAIR -> RETEST -> APPROVED -> PR -> CHECKS -> MERGE.
- Gates: acceptance criteria, safety invariants, relevant tests, reviewer approval, exact-head green checks.
- Retry/escalation: repair and rereview; after three same-cause failures run root-cause analysis and replan; escalate only unresolved authority/external dependency.
- Stop/outputs: stop on merge or documented blocker; retain plan, test/review evidence, PR/SHA, learning.
- Budget/human boundaries: avoid needless broad builds; user alone supplies credentials, broker consent, real-trading unlock, and destructive-risk-policy decisions.
