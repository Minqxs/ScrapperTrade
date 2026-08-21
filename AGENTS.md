# ScrapperTrade Agent Guide

Read this file, `.agentic/build-state/build-state.json`, and `docs/agent-learning/PROJECT_LEARNINGS.md` before changing the repository.

## Non-negotiable invariants

1. Never send an automated order unless MT5 positively reports a DEMO account. REAL, CONTEST, UNKNOWN, disconnected, stale, or undetermined accounts fail closed.
2. Authority is `User -> Permissions -> Hard Risk Policy -> Portfolio Risk Engine -> Strategy Engine -> Execution Safety Gate -> MT5 EA -> Broker`. AI operates below user permissions and cannot weaken or unlock any boundary.
3. LLMs and research processes never sit in the tick-to-order hot path and never call execution. Only validated, versioned strategy specifications may reach deterministic runtime code.
4. Emergency lock requires explicit user reactivation. Pause blocks new entries while protective management continues.
5. Never claim profitability. Evidence, costs, out-of-sample performance, and forward performance remain distinct from engineering acceptance.
6. Runtime secrets, databases, credentials, private media, and transcripts do not belong in Git.

## Working protocol

- Work in the current bucket integration branch `run/<bucket>`; coherent tasks use `task/<bucket>/<task>` and target the integration branch. After bootstrap, do not implement on `main`.
- Inspect repository state before acting. Preserve unrelated user changes. Update machine-readable build state only for facts you verified.
- A task is done only when implemented, tested, run where applicable, reviewed, repaired, integrated, and documented. UI work also needs visual QA; financial logic needs deterministic tests.
- After about three repetitions of the same failure, stop retrying and perform root-cause diagnosis/replanning.
- Use simulator/test doubles when human-only MT5 or provider setup is unavailable. Mark only the external verification pending and continue independent work.
- Record durable discoveries and failed approaches in `docs/agent-learning/PROJECT_LEARNINGS.md`; use ADRs for material decisions.
- Never weaken a safety test, bypass failing CI, force-push `main`, or report an unperformed MQL5 compile as passed.

## Ownership and boundaries

Role responsibilities are in `.agentic/agents/`. Loop contracts are in `.agentic/loops/`. Product intent is in `docs/product/PRODUCT_SPEC.md`; technical boundaries are in `docs/architecture/ARCHITECTURE.md`. More specific nested `AGENTS.md` files, if later introduced, apply within their directory but cannot weaken these invariants.

## Verification evidence

Build-state test/review fields must name the exact command or external check, outcome, timestamp, and commit SHA. `pending-external` is valid; invented success is not.
