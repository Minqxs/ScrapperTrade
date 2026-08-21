# UI Visual Loop

- Trigger: user-visible change. Objective: safe, coherent, responsive and accessible control centre.
- Agents/tools: UI/UX, Visual QA, QA, Reviewer; running app, Playwright, screenshots, accessibility tooling.
- States: DEFINE STATES -> IMPLEMENT -> RUN -> CAPTURE DESKTOP/MOBILE -> ACCESSIBILITY -> DEFECTS -> REPAIR -> RECAPTURE -> APPROVE.
- Gates: loading/empty/error/degraded states; destructive confirmations; keyboard/focus; readable account/system state; no screenshot-only approval.
- Retries/stop: diagnose systemic layout/state issues after three recurrences; retain screenshots, traces, defect list, viewport/browser versions.
- Boundaries/budget: do not expose secrets; simulate dangerous actions; human confirmation semantics cannot be removed for convenience.
