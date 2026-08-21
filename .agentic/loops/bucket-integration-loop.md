# Bucket Integration Loop

- Trigger: all ready tasks merged into `run/<bucket>`. Objective: validate the bucket as an integrated increment.
- Agents: Orchestrator, QA, relevant specialists, Reviewer, Git/PR.
- States: RECONCILE STATE -> BUILD -> TEST -> INTEGRATION/E2E -> VISUAL/MT5 GATES -> REVIEW -> REPAIR -> AGGREGATE PR -> EXACT SHA CHECKS -> MERGE MAIN -> VERIFY/CLEANUP.
- Gates: dependencies and acceptance criteria satisfied, no blocking findings, CI green at exact head, documentation/build-state updated.
- Retry/escalation: isolate failing task/contract; after three same-cause failures diagnose/replan. External gates remain precise pending items.
- Stop/outputs: verified main SHA or blocker; retain commands, artifacts, PR, checks, merge SHA, learnings. Never force-push or merge failing CI.
