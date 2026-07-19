# Adapter Classification

## Production adapters

- `EfConversationSimulationStore`
- `EfPromptStudioRepository`
- `EfKnowledgeStudioRepository`
- `LocalKnowledgeDocumentStorage` for the current single-node deployment profile
- `RoutingIntelligenceExecutor`, `GeminiIntelligenceExecutor`, and `DeterministicIntelligenceExecutor`

## Development foundations

The legacy `Placeholder*` engines and in-memory Knowledge/Intelligence repositories support bounded contexts whose operational implementation is not yet complete. They are registered as capability foundations, not as Prompt Studio, Knowledge Studio, or Simulation persistence. They must not be mistaken for production-complete engines.

## Test doubles

In-memory repositories used by test projects stay inside test assemblies. No in-memory Conversation Simulator store is registered in the production application graph.
