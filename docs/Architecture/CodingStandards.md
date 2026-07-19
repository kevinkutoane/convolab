# Coding Standards

## Boundaries

- Domain owns invariants, lifecycle transitions, value objects, policies, and domain events.
- Application owns use cases and ports. It does not reference Entity Framework, provider SDKs, HTTP, or file-system implementations.
- Infrastructure implements persistence and external adapters without deciding business legality.
- API maps HTTP to Application contracts and emits RFC 7807 Problem Details.
- Studio contains presentation state and typed API calls only.

## C#

Use nullable reference types, immutable records for transport/state snapshots, cancellation tokens on I/O operations, explicit validation, and structured domain error codes. Avoid compressed controllers, magic strings outside mapping boundaries, and direct status mutation outside domain policies.

## TypeScript

Use strict types, the shared API client, normalized Problem Details, revision-aware mutations, accessible controls, and explicit loading/error/empty states. Never place provider secrets or platform business rules in the browser.
