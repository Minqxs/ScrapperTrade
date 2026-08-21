# ADR-0001: Local modular monolith with adapter boundaries

- Status: Accepted
- Date: 2026-08-21

## Decision

Use a loopback ASP.NET Core modular monolith with Domain, Application, Infrastructure, Quant and Host boundaries; serve a React/TypeScript UI and store initial runtime data in SQLite under per-user application data.

## Consequences

Local setup and recovery stay simple and zero-cost-first. Explicit ports preserve later VPS/process separation. Module contracts and background-job isolation are mandatory to avoid a distributed monolith in one process.
