# MT5 Integration Loop

- Trigger: IPC, EA, broker, symbol, or execution change. Objective: verified, idempotent DEMO-only integration.
- Agents: MT5/MQL5, Backend, QA, Reviewer/Skeptic.
- States: CONTRACT -> SIMULATOR -> FAILURE TESTS -> EA IMPLEMENT -> MQL5 COMPILE -> DISCONNECTED TEST -> DEMO ACCOUNT VERIFY -> DEMO SMOKE -> REVIEW.
- Gates: account positively DEMO; stale/duplicate commands rejected; atomic queue recovery; broker result reconciled; hedging/netting explicit.
- Retry/escalation: reproduce from retained commands/acks/logs; unavailable MetaEditor/login becomes `pending-external`, not passed.
- Stop/outputs: approved evidence or specific human dependency; retain protocol version, compile log, simulator tests, sanitized smoke evidence.
- Permissions/boundaries: never use REAL/CONTEST/unknown; only user may log in, accept broker terms, or enable future live gates.
