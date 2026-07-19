# Workflow Studio

Workflow Studio is the visual product surface for the Workflow Engine. It manages reusable workflow definitions independently from runtime executions.

## Capabilities

- Create persistent workflow definitions.
- Create semantic versions.
- Compose Start, Knowledge, Prompt, Decision, Intelligence, Response, and End nodes.
- Position nodes on a visual canvas.
- Connect nodes with labelled and conditional transitions.
- Validate graph invariants before governance transitions.
- Submit, approve, publish, deprecate, archive, and restore workflow versions.
- Select published workflow versions in Conversation Simulator.
- Persist the resolved workflow path with each simulation run.

## Validation rules

A publishable workflow must contain exactly one Start node and at least one End node. Every non-End node requires an outgoing transition. Decision nodes require at least two outgoing transitions. Nodes must be reachable from Start, and transitions may not target the same node that they originate from.

## Conditional branches

Workflow Designer v1 supports a deliberately small deterministic condition language:

```text
contains:hail
```

During simulation, a transition with this condition is selected when the customer message contains `hail`, case-insensitively. An unconditional transition acts as the default branch.

## Definition versus execution

Workflow definitions and published versions are governed assets. Conversation Simulator resolves a single path from a published version and stores an immutable snapshot with the run. Future durable workflow execution can replace the current simulation path resolver without changing Studio contracts.

## API

- `GET /api/workflows`
- `POST /api/workflows`
- `GET /api/workflows/{id}`
- `PATCH /api/workflows/{id}`
- `POST /api/workflows/{id}/versions`
- `PUT /api/workflows/versions/{versionId}/graph`
- `GET /api/workflows/versions/{versionId}/validate`
- `POST /api/workflows/versions/{versionId}/{action}`
- `GET /api/workflows/published`
