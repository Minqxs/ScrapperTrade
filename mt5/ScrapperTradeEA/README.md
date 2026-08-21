# ScrapperTrade EA

The EA is a broker adapter and final safety gate, never a strategy engine. It polls an atomic queue under the MT5 common-files directory and writes heartbeat, symbol-discovery, and result files there.

Trading is rejected unless all of these are true:

- MT5 is connected and reports `ACCOUNT_TRADE_MODE_DEMO`.
- The command timestamp is fresh, its sequence is monotonic, and its ID has not already been consumed.
- The EA's user-controlled `EmergencyLocked` input is explicitly disabled.
- A new order has positive volume, stop loss, and take profit.

`EmergencyLocked` defaults to `true`. The application cannot change this EA input. MetaEditor compilation remains a required local gate; source presence is not evidence that compilation passed.

```text
ScrapperTrade/
  heartbeat.json
  symbols.json
  last-command-sequence.txt
  commands/<command-id>.cmd
  results/<command-id>.json
```

The host atomically publishes command lines as `id|created-unix|action|symbol|volume|price|stop|target|ticket|sequence|expires-unix`. Result files are durable idempotency markers across EA restarts. The host also refuses expired/future-dated requests and reused IDs. Missing or malformed heartbeat evidence fails closed.
