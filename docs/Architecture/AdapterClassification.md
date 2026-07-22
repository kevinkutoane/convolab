# Adapter Classification

## Production adapters

- `EfConversationSimulationStore`
- `EfPromptStudioRepository`
- `EfKnowledgeStudioRepository`
- persisted Evaluation, Trace, Replay, Policy, and Plugin EF repositories
- `LocalKnowledgeDocumentStorage` for the current single-node deployment profile
- `RoutingIntelligenceExecutor`, `GeminiIntelligenceExecutor`, and `DeterministicIntelligenceExecutor`

## Bounded runtime state

`RuntimeIntelligenceProviderRepository` is a configuration-derived provider catalogue rebuilt idempotently at startup. `RuntimeExecutionRequestRepository` holds only an execution while it is active; completed execution history is persisted through the Studio repositories. `RuntimeExecutionBudgetRepository` is rebuilt from configured ZAR budgets. These adapters do not represent user-authored or historical persistence.

The retired legacy Workflow composition and no-op Conversation, Prompt, AI, Evaluation, Trace, Knowledge, and Intelligence adapters are not registered or shipped in the production graph.

## Test doubles

In-memory repositories used by test projects stay inside test assemblies. An architecture test rejects production types named `Placeholder*` or `InMemory*`.
