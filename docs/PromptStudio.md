# Functional Prompt Studio v1

Prompt Studio turns Prompt Engine artifacts into persisted, governed, executable assets.

## Capabilities

- Create prompt definitions with ownership, category and tags.
- Compose ordered System, Developer, Knowledge, Conversation, User and Output sections.
- Discover `{{variable}}` references and estimate tokens.
- Create immutable semantic versions.
- Govern versions through Draft, PendingApproval, Approved, Published, Deprecated and Archived states.
- Render a version with test variables and inspect missing variables.
- Compare versions by token count and variable changes.
- Expose only published versions to Conversation Simulator.
- Persist the exact rendered prompt snapshot with every simulation run.

## Runtime variables

The simulator supplies `customerMessage`, `knowledgePackage`, `conversationHistory`, `workflow`, `knowledgeCollection` and `promptVersion`.

Retrieved knowledge is deliberately wrapped by prompt authors in explicit knowledge delimiters. Documents remain untrusted data and must never override system instructions.

## API

- `GET /api/prompts`
- `POST /api/prompts`
- `GET /api/prompts/{id}`
- `PATCH /api/prompts/{id}`
- `POST /api/prompts/{id}/versions`
- `POST /api/prompts/versions/{versionId}/{action}`
- `POST /api/prompts/render`
- `GET /api/prompts/compare?left={id}&right={id}`
- `GET /api/prompts/published`

## Current limitations

- Section editing is performed before a version is created; versions themselves are immutable.
- Comparison currently reports token and variable deltas rather than a line-level diff.
- Environment promotion, collaborative editing and approval identities will follow authentication and tenancy.
