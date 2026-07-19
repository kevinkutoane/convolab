# Capability Dependency Matrix

`Allowed` means the capability may hold references or consume a published contract. It does not grant permission to reach into another aggregate.

| Consumer | Conversation | Workflow | Prompt | Knowledge | Intelligence | Policy | Evaluation | Tracing | Plugins |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Conversation | — | Reference only | Reference only | Reference only | No | Reference only | Reference only | Reference only | No |
| Workflow | Reference only | — | Contract | Contract | Contract | Contract | Reference only | Contract | Contract |
| Prompt | Reference only | Reference only | — | `KnowledgePackage` only | No | Contract | Reference only | Event only | Contract |
| Knowledge | Context reference | Workflow reference | No | — | No | Contract | Event only | Event only | Contract |
| Intelligence | Context reference | Execution reference | Rendered prompt | `KnowledgePackage` | — | Contract | Contract | Contract | Contract |
| Policy | Published facts | Published facts | Published facts | Published facts | Published facts | — | No | Event only | No |
| Evaluation | References | References | References | Citations | Execution result | Policy thresholds | — | Contract | Contract |
| Tracing | Correlation | Correlation | Artifact | Artifact | Spans/events | Decision reference | Result reference | — | Adapter metadata |
| Plugins | Public contracts | Public contracts | Public contracts | Public contracts | Public contracts | Public contracts | Public contracts | Public contracts | — |

## Forbidden dependencies

- Domain capability to API, Infrastructure, React, ASP.NET Core, EF Core, or vendor SDKs.
- Conversation to provider or model implementation.
- Prompt to retriever implementation.
- Knowledge to prompt rendering.
- Workflow to provider retry or fallback logic.
- Studio to Domain assemblies or persistence repositories.
- Infrastructure implementation to another Infrastructure implementation without an explicit adapter contract.

## Integration rule

Cross-capability interaction uses one of:

1. immutable identifiers or references;
2. public Application contracts;
3. published domain or integration events;
4. sealed transfer artifacts such as `KnowledgePackage` or `ExecutionPlan`.
