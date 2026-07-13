# 0005-rename-pipeline-to-workflow

## Status
Accepted

## Context
During Sprint 1, the concept of a "Pipeline" was introduced to represent the sequence of operations involved in processing a conversational AI request. As the domain model evolved and the understanding of the system's core responsibilities deepened, it became apparent that the term "Pipeline" did not fully capture the nuanced meaning of the orchestrated business process. The term "Workflow" is more aligned with the Ubiquitous Language of enterprise process orchestration and better reflects the stateful, dynamic, and potentially branching nature of the execution flow.

## Decision
The term "Pipeline" will be replaced with "Workflow" across the entire codebase, documentation, and architectural discussions. This includes renaming classes, interfaces, enums, value objects, and diagram labels.

## Consequences
*   **Improved Ubiquitous Language**: The term "Workflow" more accurately describes the business process, fostering better communication between domain experts and developers.
*   **Enhanced Clarity**: The new terminology better conveys the intent of the system, which is to orchestrate complex, multi-step processes rather than simple linear data flows.
*   **Code Refactoring**: All occurrences of "Pipeline" in code (class names, variable names, method names) will be updated to "Workflow".
*   **Documentation Updates**: All existing documentation, including ADRs, review documents, and diagrams, will be updated to reflect the new terminology.
*   **Potential for Breaking Changes**: While efforts will be made to minimize impact, this change may require adjustments in dependent components during the transition phase.
*   **Alignment with Industry Standards**: "Workflow" is a widely recognized term in business process management and orchestration, aligning ConvoLab with established enterprise patterns.
