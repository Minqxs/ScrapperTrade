# MT5 setup

1. Install MetaTrader 5 from the chosen broker and create/log into a DEMO account.
2. Copy `mt5/ScrapperTradeEA/ScrapperTradeEA.mq5` into the terminal's `MQL5/Experts/ScrapperTradeEA` folder.
3. Compile it in MetaEditor and resolve every error; repository source alone is not a successful compile.
4. Attach it to one chart. Confirm the heartbeat says `DEMO` before unlocking anything.
5. Keep `EmergencyLocked=true` until the simulator and risk configuration have been reviewed.

REAL, CONTEST, unknown, disconnected, or stale account state is rejected. Never use development verification on a funded account.

