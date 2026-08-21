# ScrapperTrade

ScrapperTrade is a local-first algorithmic trading workbench for Windows. The current release is a safety-first simulator vertical slice: it provides a React control centre, deterministic .NET risk/quant components, an auditable DEMO-only simulator, and the source for a fail-closed MT5 execution EA.

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

The required baseline is .NET 8 and Node.js/npm. The scripts provide actionable diagnostics when a prerequisite is missing. Runtime audit data lives under `%LOCALAPPDATA%\ScrapperTrade`, outside the repository.

## What is implemented

- Explicit stopped/running/paused/emergency-locked state and user-only unlock.
- Demo-only, stale-command-safe, idempotent execution simulator.
- Independent portfolio risk checks and broker-metadata-aware sizing.
- EMA/ATR primitives, constrained strategy specifications, and a deterministic conservative backtester.
- Local API, atomic audit persistence, and responsive React operations UI.
- MT5 EA source using Common Files IPC with a second positive-DEMO gate.
- Agent roles, delivery/review loops, ADRs, and resumable 15-bucket roadmap.

This is not the completed 15-bucket platform. SQLite/EF persistence, full MT5 bridge parity, live market history, autonomous scheduling, media ingestion/transcription, AI-provider automation, champion/challenger governance, and full Playwright/release validation remain roadmap work. The machine-readable source of truth is [`.agentic/build-state/build-state.json`](.agentic/build-state/build-state.json).

## Verification

```powershell
dotnet build .\ScrapperTrade.sln
dotnet run --project .\tests\ScrapperTrade.Tests\ScrapperTrade.Tests.csproj
Set-Location .\web\scrappertrade-ui
npm.cmd test -- --run
npm.cmd run build
```

MetaEditor is not bundled. Follow [MT5 setup](docs/operations/MT5_SETUP.md) and compile the EA locally before considering MT5 integration verified.

## Documentation

- [Product specification](docs/product/PRODUCT_SPEC.md)
- [Architecture](docs/architecture/ARCHITECTURE.md)
- [Local setup](docs/operations/LOCAL_SETUP.md)
- [DEMO trading](docs/operations/DEMO_TRADING.md)
- [Troubleshooting](docs/operations/TROUBLESHOOTING.md)
