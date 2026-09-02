# v1.0.0-alpha.18 — Security & Compliance Hardening Plan

> **Planning only.** This document is based on the authoritative current `main` baseline `b3f6bfe4c6be7fd47ec407d95a0ce7ba1bb22242`. None of the items below are implemented by the alpha.17 closure changes.

## Objectives

Alpha.18 should close the remaining substantiated security risks at integration boundaries while preserving the current architecture. The work should proceed from evidence, use the smallest required change, and add negative tests before declaring a control effective.

| Priority | Item | Current state | Risk | Complexity |
|---|---|---|---|---|
| P0 | Workspace/resource isolation | Global resource routes and object-only persistence queries remain insufficiently proven for multi-tenant isolation | Cross-workspace disclosure or mutation | High |
| P0 | Plugin SSRF closure | Scheme, DNS, and private-address checks exist; redirect and connect-time destination enforcement are not fully proven | Internal network access or metadata exposure | Medium |
| P1 | Secret cache controls | Resolved secret strings may remain in process memory for the configured cache TTL | Host memory-dump or operator exposure | Medium |
| P1 | Telemetry and logging deny-list | Automatic instrumentation suppression exists in selected paths; future custom logging remains a leakage risk | API keys, client secrets, or prompts in logs/traces | Medium |
| P1 | OIDC error-path secrecy | Client secret is injected at token exchange time and tokens are not saved | Error serialization could disclose exchange material | Low–Medium |
| P1 | Sensitive trace reveal boundary | Sensitive content is redacted by default but reveal is exposed as a read flag | Privileged data disclosure | Medium |
| P2 | Secret-reference canonicalization | Canonicalization exists but edge cases need explicit coverage | Cache collisions or failed invalidation | Low |
| P2 | Break-glass operations | Runtime controls exist; rotation and audit procedures need operational evidence | Persistent emergency access | Low |
| P2 | Security CI and release evidence | Reveal guard and release workflow exist; current-SHA execution evidence is missing | Regression and supply-chain assurance gaps | Medium |
| P2 | Deployment-default hardening | Local Compose includes development credentials and published database port | Accidental exposure or credential reuse | Medium |

## Proposed work items

### A18-01 — Enforce resource-level workspace authorization

**Objective.** Ensure every tenant-owned read, list, update, delete, replay, export, health, and reveal operation is authorized against the target resource’s workspace and organisation.

**Evidence.** Global simulation, trace, plugin, and intelligence routes do not consistently carry workspace context, while persistence methods include object-only lookup patterns.

**Proposed change.** Add workspace keys to repository contracts and predicates; use workspace-scoped routes or a resource authorization handler; reject service-account requests outside the account workspace.

**Affected components.** API controllers, application services, repository interfaces, EF repositories, authorization policies, integration-test fixtures.

**Tests and acceptance criteria.** Two isolated workspaces must be provisioned. A user or service account from workspace A must receive 403/404 and no data for every workspace B operation. Positive same-workspace operations must remain functional. Audit events must retain the correct workspace.

### A18-02 — Close plugin SSRF redirect and rebinding paths

**Objective.** Ensure plugin health checks cannot reach private, local, link-local, ULA, or metadata destinations through DNS changes or redirects.

**Evidence.** The current probe performs a DNS precheck and then sends an HTTP request; redirect behavior and connect-time destination validation require explicit proof.

**Proposed change.** Disable automatic redirects or manually validate every hop; enforce a bounded hop count and destination policy; use a handler or egress proxy that validates the actual connection target; cap response sizes.

**Affected components.** `HttpPluginHealthProbe`, named `HttpClient` registration, plugin integration tests, egress policy.

**Tests and acceptance criteria.** Tests cover redirect-to-loopback, redirect-to-link-local, metadata IPs, IPv4-mapped IPv6, alternate IP encodings, DNS rebinding, excessive redirects, and oversized responses.

### A18-03 — Reduce secret-memory exposure

**Objective.** Minimize the lifetime of resolved secrets in application memory.

**Evidence.** Secret caching is configurable and the current development configuration uses a five-minute cache TTL.

**Proposed change.** Use a conservative production default, support explicit no-cache references, invalidate on rotation, and document diagnostic-dump restrictions.

**Affected components.** Composite secret store, secret-reference contracts, configuration, operations documentation.

**Tests and acceptance criteria.** No-cache references never enter the cache; rotation invalidates prior values; production configuration rejects unsafe TTLs or requires an explicit exception.

### A18-04 — Establish logging and telemetry secret policy

**Objective.** Guarantee that secret values, authorization headers, client secrets, and sensitive prompts do not enter logs, traces, analytics, or exception responses.

**Evidence.** Selected provider calls suppress automatic instrumentation, but the protection is distributed across call sites.

**Proposed change.** Add centralized header/attribute redaction, a narrow `RevealValue()` allow-list, and failure-path tests.

**Affected components.** Telemetry middleware, provider adapters, OIDC events, CI scripts, analytics serializers.

**Tests and acceptance criteria.** Induced failures in OIDC, provider validation, and Gemini execution produce no secret-bearing log or telemetry record. CI fails on new unsafe reveal sites.

### A18-05 — Make sensitive trace reveal explicit and privileged

**Objective.** Separate ordinary trace retrieval from sensitive-content disclosure.

**Evidence.** The current read path accepts `includeSensitive=true` while mapping sensitive artifacts to original content when enabled.

**Proposed change.** Create a dedicated reveal operation with permission, workspace authorization, feature/retention gates, reason capture, and an audit event.

**Affected components.** Trace controller/service, authorization policy, runtime settings, analytics/audit.

**Tests and acceptance criteria.** Unauthorized, cross-workspace, disabled-feature, and missing-reason requests fail; authorized reveals are audited and ordinary GET responses remain redacted.

### A18-06 — Complete canonicalization and break-glass assurance

**Objective.** Prove deterministic secret reference handling and operational emergency-access controls.

**Evidence.** Canonicalization and break-glass controls exist but require edge-case and operational evidence.

**Proposed change.** Add normalization tests and a rotation/audit runbook with ownership, expiry, and incident review.

**Acceptance criteria.** Equivalent references map to one canonical key; invalidation removes all equivalent cache entries; break-glass credentials have documented rotation and review evidence.

### A18-07 — Harden deployment defaults and produce supply-chain evidence

**Objective.** Prevent accidental use of local credentials in shared environments and complete the current-baseline release evidence chain.

**Evidence.** Local Compose uses development credentials and publishes PostgreSQL; the release workflow exists but a current-SHA artifact bundle was not available during closure.

**Proposed change.** Remove non-test fallbacks from deployment profiles, use scoped database credentials, avoid public database ports, and run the release workflow for the authoritative SHA.

**Acceptance criteria.** Production configuration fails closed on missing secrets; no deployment manifest publishes PostgreSQL; CI stores image digests, SBOMs, Trivy results, provenance, and a manifest linked to the source SHA.
