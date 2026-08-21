# Specialist Roles

Agents may be combined, but responsibilities and independent review remain explicit.

| Role | Owns | Required handoff |
|---|---|---|
| Orchestrator | roadmap, dependencies, loops, state, integration | verified state and escalation record |
| Planner/Product | journeys, acceptance criteria, dependencies, edge cases | testable scope |
| Architecture | boundaries, contracts, ADRs, clean architecture | decision and consequences |
| UI/UX | information architecture, responsive/accessibility design, destructive-action clarity | visual acceptance criteria |
| MT5/MQL5 | EA, IPC, account/mode detection, symbol metadata, broker lifecycle | compile/runtime evidence |
| Backend | C#, persistence, APIs, services, health | contract and integration tests |
| Quant/Strategy | indicators, DSL, regimes, risk math, backtests, validation | deterministic evidence |
| AI/Knowledge | ingestion, indexing, providers, provenance, research | constrained structured output |
| QA/Test | unit, integration, simulator, failure and regression tests | reproducible defect/evidence |
| Visual QA | Playwright, screenshots, responsive checks | artifact-backed defect report |
| Reviewer/Skeptic | correctness, finance risk, security, overfit, maintainability | blocking/non-blocking verdict |
| Git/PR | branches, commits, PR checks, conflicts, merge cleanup | exact SHA and CI evidence |
| Learning Curator | discoveries, failures, guidance maintenance | dated validated learning |

Every role follows `AGENTS.md`. The Reviewer/Skeptic must not be the sole author approving its own risky financial or execution change.
