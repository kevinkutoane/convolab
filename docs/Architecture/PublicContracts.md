# Public Capability Contracts

## Conversation

Capabilities: create, start, pause, resume, complete, archive, manage sessions and participants, add immutable messages, update memory, attach workflow/evaluation/trace references, and read timeline data.

Publishes facts such as `ConversationStarted`, `MessageAdded`, `SessionEnded`, `MemoryUpdated`, and `TraceLinked`.

## Workflow and Execution

Capabilities: define versioned workflows, create executions, progress legal state transitions, cancel, fail, complete, and publish execution facts. Workflow owns business progression; it does not select models or implement provider fallback.

## Prompt

Capabilities: create governed prompt assets, render templates, compose sections, validate variables, create immutable versions, approve, reject, archive, restore, compare, roll back, and create experiments.

## Knowledge

Capabilities: register sources and connectors, publish governed documents, execute provider-neutral queries, rank results, create sealed `KnowledgePackage` artifacts, create snapshots, version knowledge, and enforce classification and retention.

## Intelligence

Capabilities: plan provider-neutral execution, match required capabilities, enforce execution budgets, select retry/fallback policies, manage streaming and tool invocation contracts, normalize results, and publish usage and cost.

## Policy

Capabilities: evaluate contextual facts against versioned governance rules and return explicit policy decisions. Policy does not execute the governed operation.

## Evaluation

Capabilities: evaluate execution or conversational artifacts against versioned scorecards and thresholds and publish normalized results.

## Tracing

Capabilities: create traces and nested spans, append events and artifacts, correlate capability activity, record metrics, and complete or fail traces.

## Plugins

Capabilities: register extension metadata, expose supported capabilities, validate compatibility, monitor health, and manage lifecycle. Plugins implement public contracts and remain replaceable.

## Studio API

The first Studio-facing contract is:

- `GET /api/platform/status` — returns platform version, environment, architecture health, and capability inventory.

Future Studio endpoints must expose application DTOs, never domain entities.
