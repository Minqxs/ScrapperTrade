# ADR-0004: User-owned safety authority and fail-closed DEMO execution

- Status: Accepted
- Date: 2026-08-21

## Decision

Enforce the authority hierarchy documented in `AGENTS.md`. Automated broker actions require current positive DEMO evidence at the host and EA gates. AI cannot change hard policy, permissions, emergency lock, instrument allowances, or lifecycle promotion authority.

## Consequences

Uncertainty sacrifices entries for safety. Protective/risk-reducing actions need explicit, tested degraded-mode rules. Future real trading is a separate user-authorized design and cannot be enabled by configuration optimization or research.
