# 0007-workflow-definition-and-execution

## Status
Accepted

## Context
As ConvoLab evolves to support complex conversational AI scenarios, the need for defining and managing reusable workflow structures has become critical. The initial `Workflow` aggregate (formerly `Pipeline`) primarily focused on a single execution instance. To enable reusability, versioning, and a clear separation between the blueprint of a workflow and its runtime execution, a more structured approach is required.

## Decision
Two distinct aggregate roots will be introduced:
1.  **`WorkflowDefinition`**: This aggregate will represent the blueprint of a workflow. It will contain the static structure, steps, and configuration required to execute a specific type of conversational AI process. It will support versioning to allow for iterative improvements and backward compatibility.
2.  **`WorkflowExecution`**: This aggregate will represent a single, concrete instance of a workflow run based on a specific `WorkflowDefinition` version. It will manage the runtime state, progress, and outcome of that particular execution.

This separation ensures that workflow definitions can be managed independently of their executions, promoting reusability and maintainability.

## Consequences
*   **Clear Separation**: A clear distinction between the static definition of a workflow and its dynamic execution instance.
*   **Version Control**: `WorkflowDefinition` will support versioning, allowing for updates to workflow logic without affecting ongoing executions or requiring immediate migration.
*   **Reusability**: Workflow definitions can be reused across multiple `WorkflowExecution` instances.
*   **Increased Complexity**: Managing two distinct aggregates for definition and execution introduces additional complexity in the domain model and persistence layer.
*   **Data Model Changes**: Requires updates to the data model to accommodate `WorkflowDefinition` and `WorkflowExecution` aggregates and their relationships.
