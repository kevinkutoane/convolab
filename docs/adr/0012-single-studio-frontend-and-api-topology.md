# ADR 0012: Single Studio Frontend and Platform API Topology

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

The repository temporarily contained two React applications and two backend stacks: the established ASP.NET Core Platform API plus a generated Express/tRPC/Drizzle server and Manus-specific frontend. This created duplicate routing, persistence, authentication, build tooling, and product ownership.

## Decision

- `web/` is the only ConvoLab Studio frontend.
- `src/Api/ConvoLab.Api` is the only platform backend.
- The generated `client/`, `server/`, `shared/`, Drizzle, Manus runtime, and template integration code are removed.
- Studio communicates with the Platform API through versionable DTO contracts.
- Business capability logic remains in Domain and Application, never Studio.
- Provider and enterprise integrations enter through Infrastructure or plugins.

## Consequences

### Positive

- One coherent build and deployment topology.
- No duplicated business or persistence logic.
- Studio reflects the established Clean Architecture boundaries.
- Easier testing, onboarding, and CI.

### Trade-offs

- Features provided by the generated stack must be intentionally reintroduced through ConvoLab architecture when needed.
- Authentication, notifications, storage, and provider integrations require explicit adapters rather than template shortcuts.
