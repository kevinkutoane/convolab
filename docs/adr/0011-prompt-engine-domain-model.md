# ADR-0011: Prompt Engine Domain Model

**Status:** Accepted  
**Date:** 2026-07-16  
**Author:** Kevin Kutoane  

## Context

The ConvoLab platform requires a first-class Prompt Engine to manage the full lifecycle of AI prompts — from authoring and versioning through approval governance and A/B experimentation. Prompts are a core business asset in any AI-powered enterprise platform and must be treated with the same rigour as code: versioned, reviewed, approved, and auditable.

## Decision

We model the Prompt Engine as a dedicated bounded context within the ConvoLab domain, exposing two aggregate roots:

1. **`Prompt`** — the primary aggregate, owning versioning, approval workflow, rendering, composition, and variant management.
2. **`PromptExperiment`** — a secondary aggregate managing A/B test lifecycle independently of the Prompt aggregate.

Key design decisions:

- **Semantic Versioning** (`SemanticVersion` value object) is used for all prompt versions, following `MAJOR.MINOR.PATCH` semantics.
- **Approval Workflow** is encoded directly in the `Prompt` aggregate state machine: `Draft → InReview → Approved → Active → Deprecated → Archived`.
- **Composition** is handled by a `PromptCompositionService` domain service that assembles ordered `PromptSection` entities (System, Role, Knowledge, Safety, ConversationMemory, UserMessage).
- **Governance** rules are extracted into `PromptGovernancePolicy`, a static domain service that enforces cross-cutting invariants (production approval requirements, variable validation, variant weight validation).
- **Rollback** creates a new version with the content of a previous version, preserving full audit history.

## Consequences

- All prompt mutations raise domain events, enabling full audit trails and downstream reactions.
- The `IPromptRepository` and `IPromptExperimentRepository` interfaces keep the domain free of infrastructure concerns.
- Architecture tests enforce that all Prompt entities inherit from `BaseEntity<T>`, all value objects from `ValueObject`, and all events implement `IDomainEvent`.
