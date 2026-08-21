# ScrapperTrade

ScrapperTrade is a local-first algorithmic trading workbench for Windows. It provides a React control centre, deterministic .NET risk/quant components, SQLite persistence, an auditable simulator and shadow scheduler, offline knowledge search, and a fail-closed MT5 execution EA.

It does **not** promise profitability and must not be used with real money. MT5 execution remains locked unless both the host and EA positively verify a connected DEMO account; the EA's emergency input also defaults to locked.

## Quick start

From PowerShell:

```powershell
.\scripts\setup.ps1
.\scripts\doctor.ps1
.\scripts\start.ps1
```

If Windows enforces signed scripts, invoke each with `powershell.exe -ExecutionPolicy Bypass -File`, for example `powershell.exe -ExecutionPolicy Bypass -File .\scripts\doctor.ps1`.

Open `http://127.0.0.1:5173`. Stop the local processes with:

```powershell
.\scripts\stop.ps1
```

The required baseline is .NET 8 and Node.js/npm. The scripts provide actionable diagnostics when a prerequisite is missing. Runtime databases, audit state, and knowledge files live under `%LOCALAPPDATA%\ScrapperTrade`, outside the repository. An OpenAI API key is optional; see [OpenAI setup](docs/operations/OPENAI_SETUP.md).

## What is implemented

- Explicit stopped/running/paused/emergency-locked state and user-only unlock.
- Demo-only, stale-command-safe simulation and a restart-safe shadow scheduler with no broker-command dependency.
- Portfolio/correlated-exposure risk, user-owned permissions, market regimes, and broker-aware sizing.
- Constrained strategies with costed backtests, chronological splits, walk-forward, and robustness validation.
- EF Core/SQLite migrations for trading, knowledge, research evidence, and explicit user governance.
- Safe local text/Markdown/CSV/JSON ingestion with FTS5 search, citations, retention, and deletion.
- Manual ChatGPT research plus an optional Responses API adapter with no execution authority.
- React portfolio, strategy, backtest, knowledge, research, health, recovery, audit, and provider workspaces.
- MT5 heartbeat, symbols, snapshots, close/cancel protocol, reconciliation, and a second positive-DEMO gate.
- Agent roles, delivery/review loops, ADRs, and resumable 15-bucket roadmap.

Development remains active. Broker transmission is deliberately locked while media transcription, operational hardening, browser E2E, and clean-checkout release validation are completed. The machine-readable source of truth is [`.agentic/build-state/build-state.json`](.agentic/build-state/build-state.json).

## Verification

```powershell
dotnet build .\ScrapperTrade.sln
dotnet run --project .\tests\ScrapperTrade.Tests\ScrapperTrade.Tests.csproj
dotnet test .\tests\ScrapperTrade.Infrastructure.Tests\ScrapperTrade.Infrastructure.Tests.csproj
Set-Location .\web\scrappertrade-ui
npm.cmd test -- --run
npm.cmd run lint
npm.cmd run build
```

MetaEditor is not bundled. Follow [MT5 setup](docs/operations/MT5_SETUP.md) and compile the EA locally before considering MT5 integration verified.

## Documentation

- [Product specification](docs/product/PRODUCT_SPEC.md)
- [Architecture](docs/architecture/ARCHITECTURE.md)
- [Local setup](docs/operations/LOCAL_SETUP.md)
- [DEMO trading](docs/operations/DEMO_TRADING.md)
- [Troubleshooting](docs/operations/TROUBLESHOOTING.md)
