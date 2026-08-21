# Review Repair Loop

- Trigger/objective: implementation seeks approval; discover and remove blocking defects.
- Scope/agents: changed behavior and affected boundaries; Reviewer, owner, QA, Architecture/Security specialist as needed.
- States: REVIEW -> CLASSIFY -> REPAIR -> FOCUSED TEST -> REGRESSION TEST -> REREVIEW -> APPROVED.
- Gates: every finding has severity, evidence, owner, disposition; no ignored blockers.
- Retry: three same-root-cause revisions trigger diagnostic reproduction and replanning, not identical retries.
- Stop/escalation/outputs: approval or specific external blocker; retain findings, test commands, artifacts, decision. Reviewer cannot authorize weakening hard safety.
- Budget/tools: use minimal reproducible checks first; repository read/write only unless broader permission is explicit.
