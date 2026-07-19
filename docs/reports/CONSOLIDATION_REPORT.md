# Platform Consolidation and Studio Foundation Report

## Scope

This milestone consolidates the repository around one frontend and one backend and completes the first usable ConvoLab Studio shell.

## Architecture decisions implemented

- `web/` is the sole ConvoLab Studio frontend.
- ASP.NET Core is the sole platform backend.
- Generated Manus-specific frontend, Express/tRPC server, Drizzle persistence, reference templates, and runtime plugins were removed.
- .NET target frameworks, Docker images, and documentation are aligned on .NET 8.
- `global.json` now rolls forward within the latest installed .NET 8 feature band.
- ADR 0012 records the selected topology.

## Studio delivered

- Dark-first premium engineering shell
- Responsive and collapsible navigation
- Dashboard showing Platform Core capability state
- Capability workspaces for Conversation, Workflow, Prompt, Knowledge, Intelligence, Policy, Evaluation, Tracing, Replay, Plugins, Analytics, and Settings
- Command palette with `Ctrl/Cmd + K`
- Theme switcher
- Status bar
- Meaningful capability-specific empty states
- Design-time status fallback when the API is not running
- Live Platform API integration through `GET /api/platform/status`
- Docker-friendly Nginx API proxy and SPA routing

## Governance delivered

- Architecture Handbook index
- Capability Dependency Matrix
- Public Capability Contracts
- Architecture Fitness Functions
- Product Readiness Assessment
- Versioning and Compatibility policy
- Platform Core alpha release notes
- Updated Platform Manifest, Context Map, Capability Map, Roadmap, and root README

## Validation performed

```text
npm ci        PASS
npm run lint  PASS
npm run build PASS
```

The review environment did not provide the .NET SDK, so .NET compilation and tests were not executed locally. GitHub Actions now runs Platform build, Domain tests, Architecture tests, Studio lint, and Studio build on pushes and pull requests.

## Next product milestone

Connect real Application contracts to the Studio through versioned API endpoints, beginning with Conversation Explorer or Prompt Studio. Do not add business orchestration to React components.
