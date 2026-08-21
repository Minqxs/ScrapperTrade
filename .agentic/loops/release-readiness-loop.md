# Release Readiness Loop

- Trigger: all buckets complete or release candidate requested. Objective: prove clean-checkout operability.
- Agents: Orchestrator, all assurance roles, relevant specialists, Git/PR.
- States: CLEAN CLONE -> SETUP -> BUILD -> UNIT -> INTEGRATION -> FRONTEND -> PLAYWRIGHT -> SIMULATOR E2E -> MQL5 COMPILE -> DEMO SMOKE -> SECURITY -> ARCHITECTURE -> SKEPTIC -> REPAIR -> FULL REPEAT -> DOCS/NOTES -> TAG.
- Gates: no secrets/known blockers, exact reproducible evidence, operations docs current, only unavoidable human actions pending.
- Retry/stop: any repair restarts affected gates and then the full final sequence; recurring failure triggers root-cause redesign. Stop only on validated release or explicit blocker.
- Outputs/boundaries: clean-clone log, test/visual/compile/smoke evidence, audits, release notes, tag/SHA. User supplies broker login/consent and any optional provider credentials; no real-money test.
