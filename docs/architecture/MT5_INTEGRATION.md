# MT5 Integration

The ScrapperTrade EA is a thin execution adapter and final safety gate. The default IPC is an atomic file queue in the MT5 Common Files directory; no third-party DLL is required.

Every envelope carries protocol version, command ID, correlation/trade/strategy identifiers, monotonic sequence, created/expiry timestamps, requested action and payload. Producer writes a temporary file then atomically renames it. EA validates schema/version, freshness, order, duplicates, emergency state, account type/connection and symbol metadata, then writes an immutable acknowledgement/result. Consumed markers and a bounded idempotency ledger make restart safe.

Automated order commands require fresh positive account evidence of DEMO. REAL, CONTEST, UNKNOWN, disconnected or undetermined rejects. Future real support needs multiple user-controlled gates and is outside initial operation.

Hedging permits separately attributed same-symbol positions within policy. Netting uses one broker net position and must either maintain explicit internal sub-allocation or reject incompatible concurrent strategies; the selected behavior must be visible. Broker symbols are discovered and mapped to logical instruments before enablement.

Simulator contract parity precedes EA smoke tests. MQL5 compilation and DEMO terminal verification are separate evidence gates; absence of MetaEditor/login is recorded as pending external.
