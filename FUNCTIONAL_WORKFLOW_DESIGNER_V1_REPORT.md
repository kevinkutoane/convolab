# Functional Workflow Designer v1 — Implementation Report

## Delivered

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

## Validation completed in this environment

- `npm run lint` — passed
- `npm run build` — passed
- `npm run test -- --run` — passed

## Runtime validation

The user confirmed the preceding hardened Docker release starts successfully. This environment does not contain the .NET SDK or Docker CLI, so the new workflow migration and backend tests still need to run in the user's Docker environment.

## Suggested acceptance flow

1. Rebuild Docker.
2. Open `/workflows`.
3. Create `Claims Intake`.
4. Create version `1.0.0` with the starter graph.
5. Validate, submit, approve, and publish it.
6. Create a simulation using `Claims Intake v1.0.0`.
7. Send a customer message.
8. Inspect the workflow snapshot and resolved path in the run overview and trace.
9. Restart containers and confirm the workflow remains available.
