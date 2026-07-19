# Platform Core v1.0.0-alpha

## Summary

This alpha establishes the first stable architecture baseline for ConvoLab Platform Core and introduces the ConvoLab Studio foundation.

## Included capabilities

- Conversation Engine
- Workflow and Execution
- Prompt Engine
- Knowledge Engine
- Intelligence Engine
- Policy, Evaluation, Tracing, Plugin, and Identity foundations

## Studio

- Premium dark-first Studio shell
- Responsive navigation and capability workspaces
- Command palette (`Ctrl/Cmd + K`)
- Platform dashboard and architecture health view
- Design-time fallback when the API is unavailable
- Live `GET /api/platform/status` integration

## Architecture decisions

- `web/` is the sole product frontend.
- ASP.NET Core is the sole platform backend.
- Generated Manus-specific client, server, database, and integration scaffolding has been removed.
- Studio contains no business orchestration.
- .NET targets and container images are aligned on .NET 8.

## Known limitations

- No provider adapters
- No production authentication or tenant isolation
- No production persistence workflow
- Studio workspaces currently present platform capability state and meaningful empty states rather than runtime data
- Policy, Evaluation, Tracing, Plugins, and Identity remain capability foundations
