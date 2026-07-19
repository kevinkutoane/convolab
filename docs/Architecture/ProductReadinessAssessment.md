# Product Readiness Assessment — v1 Alpha

## Decision

Platform Core is architecturally ready for Studio and adapter development. It is not yet production-ready for enterprise workloads.

## Ready

- Provider-neutral domain model
- Stable Conversation, Workflow, Prompt, Knowledge, and Intelligence boundaries
- Clean Architecture project structure
- Architecture and domain test projects
- Governance documentation and ADR history
- Single React Studio frontend
- Single ASP.NET Core backend topology
- Studio dashboard, navigation, command palette, responsive shell, and meaningful capability workspaces
- Platform status endpoint and design-time fallback

## Experimental

- Policy behaviour
- Evaluation behaviour
- Trace persistence and export
- Plugin discovery and loading
- Identity and tenant boundaries
- Studio product interactions beyond shell and empty states

## Not implemented

- Production persistence and migration workflow
- Provider adapters
- Enterprise knowledge connectors
- Authentication and authorization
- Secret management
- Streaming transport
- Tool execution runtime
- Production trace storage
- Deployment automation and SLOs
- Conversation simulation and replay execution

## Products that can now be built

- Conversation Explorer
- Workflow Designer
- Prompt Studio
- Knowledge Studio
- Intelligence execution inspector
- Policy Center
- Evaluation Studio
- Trace Explorer
- Conversation Simulator
- Replay Studio

## Primary risks

1. Capability foundations may drift unless architecture tests expand with new adapters.
2. Static capability metadata must eventually be generated or owned by a formal platform registry.
3. Cross-capability event delivery semantics remain undefined.
4. Security, privacy, retention, and tenant isolation require dedicated implementation before production data.
5. Replay requires immutable snapshots and redaction guarantees across all participating capabilities.
