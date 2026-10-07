# ConvoLab Documentation Portal

Welcome to the **ConvoLab** documentation. ConvoLab is an enterprise-grade conversational AI platform providing end-to-end capabilities for designing, simulating, evaluating, governing, and operating intelligent conversations.

**Current Release Candidate:** `v1.0.0-enterprise — Release Candidate`  
**Operational Status:** 100% test pass rate across all 5 test projects (526 tests passed, 0 failed).

---

## Documentation Structure

```
docs/
├── Architecture/     # Architectural guidelines, principles, fitness functions, readiness
├── adr/              # Architectural Decision Records (ADR 0001 - 0019)
├── capabilities/     # In-depth capability engine specifications
├── diagrams/         # Mermaid topology, sequence, and event-flow diagrams
├── operations/       # Operational runbooks, health checks, DR, ALM, Entra SSO
├── project/          # Developer guides, onboarding, architecture, deployment, changelog
├── releases/         # Release notes (v1.0.0-enterprise through historical alpha releases)
├── reports/          # Verification evidence, performance benchmarks, and audit reports
└── security/         # STRIDE threat models, compliance controls (SOC 2, POPIA/GDPR), checklists
```

---

## 1. Quick Start & Developer Guides

- **[Developer README](project/README.md)**: Developer onboarding, local environment setup, and running Studio.
- **[Getting Started](project/GETTING_STARTED.md)**: Step-by-step guide to cloning, configuring `.env`, and launching services.
- **[Architecture Overview](project/ARCHITECTURE.md)**: Clean Architecture layer guide and dependency rules.
- **[Deployment Guide](project/DEPLOYMENT.md)**: Docker Compose and production deployment instructions.
- **[Contributing](project/CONTRIBUTING.md)**: Development workflows, testing standards, and pull request checklist.
- **[Changelog](project/CHANGELOG.md)**: Chronological record of release milestones and changes.

---

## 2. Strategic Enterprise Pillars (`v1.0.0-enterprise`)

- **[Enterprise Release Notes](releases/PlatformCore-v1.0.0-enterprise.md)**: Complete manifest of the four strategic enterprise pillars:
  1. **Enterprise Governance, Security & Compliance**: Automated PII/PHI redaction engine ([PiiRedaction](../src/Domain/ConvoLab.Domain/Privacy/PiiRedaction.cs)), forward-linked cryptographic audit trail ([AuditHashChain](../src/Infrastructure/ConvoLab.Infrastructure/WorkspaceIdentity/AuditHashChain.cs)), and pre-execution prompt guardrails.
  2. **Omnichannel Connectors & Integrations**: Native Infobip & WhatsApp Business integration ([Omnichannel.md](Omnichannel.md)), rich messaging, human handoff protocol, and Enterprise Hybrid RAG 2.0 ([KnowledgeStudio.md](KnowledgeStudio.md)).
  3. **Multi-Environment ALM & Promotion**: Automated environment promotion pipeline (`Dev` → `Staging` → `Prod`), source health gating, and instant rollback recovery ([Deployment.md](operations/Deployment.md)).
  4. **Executive Observability, ROI & FinOps**: Executive TCO & Human Parity ROI in ZAR, unit economics, multi-dimensional attribution, and golden-dataset regression CI/CD ([PlatformAnalytics.md](PlatformAnalytics.md), [EvaluationStudio.md](EvaluationStudio.md)).

---

## 3. Platform Capabilities & Workspaces

- **[Platform Manifest](PlatformManifest.md)**: Platform mission, core aggregate boundaries, and technical constraints.
- **[Capability Map](CapabilityMap.md)**: Functional capabilities and maturity levels.
- **[Context Map](ContextMap.md)**: Domain-Driven Design bounded contexts and relationship matrix.
- **[Conversation Engine](Capability3_ConversationEngine_OperationalBehavior.md)**: Session lifecycles, memory, context, and multi-turn aggregate model.
- **[Omnichannel & WhatsApp](Omnichannel.md)**: Infobip webhook handling, rich messaging, and sentiment-based human escalation.
- **[Knowledge Studio & Hybrid RAG](KnowledgeStudio.md)**: Ingestion, chunking, and reciprocal rank fusion (BM25 + Dense Semantic RRF).
- **[Prompt Studio](PromptStudio.md)**: Governed prompt authoring, variable templating, and pre-execution shields.
- **[Workflow Designer](WorkflowStudio.md)**: Visual workflow creation and execution state machines.
- **[Intelligence Center](IntelligenceCenter.md)**: Multi-provider execution planning, token budgeting, and fallback routing.
- **[Evaluation Studio](EvaluationStudio.md)**: Automated quality scoring, evaluation batches, and regression test suites.
- **[Trace Explorer](EventCatalog.md)**: OpenTelemetry distributed conversation tracing and span inspection.
- **[Policy Center](security/ComplianceControls.md)**: Runtime policy governance, PII redaction, and prompt injection shields.
- **[Plugin Center](PluginCenter.md)**: Isolated plugin execution, compatibility verification, and health probes.
- **[Platform Analytics & FinOps](PlatformAnalytics.md)**: Cost classification, executive ROI benchmarks, and export facilities.
- **[Workspace Identity & Access](WorkspaceIdentityAccess.md)**: Multi-tenant organisation/workspace boundaries, RBAC, and audit logs.

---

## 4. Architecture & Engineering Standards

- **[Architecture Handbook](Architecture/README.md)**: Core design guidelines and engineering policies.
- **[Architecture Principles](Architecture/ArchitecturePrinciples.md)**: Foundational principles governing system evolution.
- **[Product Readiness Assessment](Architecture/ProductReadinessAssessment.md)**: Evaluation of platform maturity, operational gates, and beta criteria.
- **[Architectural Decision Records (ADRs)](adr/)**: Historical record of architectural choices (ADR 0001 - 0019).
- **[System Diagrams](diagrams/README.md)**: Component topology, dependency graphs, and sequence diagrams.
- **[Fitness Functions](Architecture/FitnessFunctions.md)**: Automated architecture testing and boundary validation.
- **[Public Contracts](Architecture/PublicContracts.md)**: REST API design conventions, error formats, and versioning.
- **[Testing Strategy](Architecture/TestingStrategy.md)**: 5-tier test architecture and verification standards.
- **[Ubiquitous Language](UbiquitousLanguage.md)**: Unified domain terminology across engineering and business domains.

---

## 5. Operations, SRE & Security

- **[Operational Health Checks](operations/HealthChecks.md)**: `/health/live`, `/health/startup`, and `/health/ready` probe semantics.
- **[Multi-Environment Deployment & ALM](operations/Deployment.md)**: Environment promotion, container images, and rollback runbooks.
- **[Backup & Disaster Recovery](operations/BackupRestore.md)**: AES-256-GCM encrypted backup creation, verification, and recovery.
- **[Disaster Recovery Runbook](operations/DisasterRecovery.md)**: RPO and RTO operating procedures and recovery drills.
- **[Authentication & Sessions](operations/Authentication.md)**: Session tokens, cookies, rate limiting, and antiforgery tokens.
- **[Microsoft Entra ID & Hybrid Auth](operations/EntraId.md)**: Enterprise single sign-on (OIDC) configuration and identity linking.
- **[Break-Glass Emergency Access](operations/BreakGlass.md)**: Procedures for emergency administrative access during outage scenarios.
- **[Safe Mode Controls](operations/SafeMode.md)**: Circuit breakers, export disabling, and degraded runtime states.
- **[Threat Model](security/ThreatModel.md)**: STRIDE analysis across API, runtime, supply chain, and AI interactions.
- **[Compliance Controls](security/ComplianceControls.md)**: SOC 2 trust services categories and POPIA/GDPR data protection controls.
- **[Production Security Checklist](security/ProductionSecurityChecklist.md)**: Mandatory verification checklist prior to production go-live.
