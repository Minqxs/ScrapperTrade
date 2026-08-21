# MT5 Integration

The ScrapperTrade EA is a thin execution adapter and final safety gate. The default IPC is an atomic file queue in the MT5 Common Files directory; no third-party DLL is required.

Every envelope carries protocol version, command ID, correlation/trade/strategy identifiers, monotonic sequence, created/expiry timestamps, requested action and payload. Producer writes a temporary file then atomically renames it. EA validates schema/version, freshness, order, duplicates, emergency state, account type/connection and symbol metadata, then writes an immutable acknowledgement/result. Consumed markers and a bounded idempotency ledger make restart safe.

Automated order commands require fresh positive account evidence of DEMO. REAL, CONTEST, UNKNOWN, disconnected or undetermined rejects. Future real support needs multiple user-controlled gates and is outside initial operation.

Hedging permits separately attributed same-symbol positions within policy. Netting uses one broker net position and must either maintain explicit internal sub-allocation or reject incompatible concurrent strategies; the selected behavior must be visible. Broker symbols are discovered and mapped to logical instruments before enablement.

Simulator contract parity precedes EA smoke tests. MQL5 compilation and DEMO terminal verification are separate evidence gates; absence of MetaEditor/login is recorded as pending external.

## Bridge slice contract

`Mt5CommonFilesHeartbeatReader` converts the atomic heartbeat into an explicit safety snapshot. Missing, malformed, disconnected, stale, future-dated, or unrecognized evidence produces a locked/unknown snapshot and cannot authorize order transmission. Account safety mode and hedging/netting position mode are separate facts.

`Mt5CommonFilesCommandQueue` validates freshness and required protective fields before writing an atomic command. It refuses an ID already present in either the commands or results directory. The EA treats an existing immutable result as a durable consumed marker across restarts, then applies expiry, monotonic sequence, positive-DEMO, and emergency-lock checks before any `CTrade` call.

`symbols.json` exposes broker names and sizing-critical metadata: tick size/value, contract size, volume bounds/step, stop level, currencies, and trade availability. Logical instrument mapping remains a user-controlled application concern.
