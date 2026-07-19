# Versioning and Compatibility

## Platform versions

ConvoLab uses semantic versioning:

- **Major:** breaking public contract or behavioural change.
- **Minor:** backward-compatible capability or contract addition.
- **Patch:** backward-compatible defect, documentation, or internal implementation fix.

Pre-release labels communicate maturity, for example `1.0.0-alpha.1`.

## Contract policy

- Domain internals are not public integration contracts.
- Application interfaces, API DTOs, events, plugin contracts, and sealed transfer artifacts are versioned public surfaces.
- Event schemas require explicit versions when external delivery is introduced.
- Deprecation must include an alternative and removal target.
- Breaking changes require an ADR and migration guidance.

## Platform Core alpha baseline

Included:

- Conversation
- Workflow and Execution
- Prompt
- Knowledge
- Intelligence
- Policy foundation
- Evaluation foundation
- Tracing foundation
- Plugin foundation

Known limitations are documented in `ProductReadinessAssessment.md`.

## Studio versions

Studio may release more frequently than Platform Core. Studio must declare the minimum compatible Platform API version and fail gracefully when a capability is unavailable.
