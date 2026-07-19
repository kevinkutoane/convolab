# Functional Workflow Designer v1 — Implementation Report

> Import status: reference only. The corresponding Workflow Designer source code was not included in the Platform Hardening Sprint 1 archive and is not part of the validated alpha.3 build.

## Delivered in the reported implementation

- Canonical workflow definition and version domain model
- Governed lifecycle and published-version immutability
- Nodes, transitions, layout coordinates, configuration, and semantic versions
- Validation for Start/End rules, unreachable nodes, dead ends, decision branches, duplicate/self transitions
- Application-layer Workflow Studio use cases and repository port
- EF Core persistence records and mapping
- Optimistic revisions on workflow definitions and versions
- Workflow lifecycle audit history
- Workflow Studio REST API
- Database migration `202607180002_WorkflowStudioV1`
- Visual Workflow Designer with draggable canvas
- Node palette and node inspector
- Transition labels and `contains:<term>` branch conditions
- Graph validation and lifecycle actions
- Published workflow discovery in Conversation Simulator
- Immutable resolved workflow snapshots stored on new simulation runs
- Backward-compatible display for legacy runs without workflow snapshots
- Domain and application tests for workflow invariants and publication

## Validation reported by the source environment

- `npm run lint` — passed
- `npm run build` — passed
- `npm run test -- --run` — passed

## Runtime validation note

The source report states that the preceding hardened Docker release starts successfully. Its environment did not contain the .NET SDK or Docker CLI, so the workflow migration and backend tests still required validation in the user's Docker environment.

## Suggested acceptance flow once source is supplied

1. Integrate the matching Workflow Designer source bundle.
2. Rebuild Docker.
3. Open `/workflows`.
4. Create `Claims Intake`.
5. Create version `1.0.0` with the starter graph.
6. Validate, submit, approve, and publish it.
7. Create a simulation using `Claims Intake v1.0.0`.
8. Send a customer message.
9. Inspect the workflow snapshot and resolved path in the run overview and trace.
10. Restart containers and confirm the workflow remains available.
