# Proposal: alpha.16 — Backup, Restore & Disaster Recovery

## Context
With the successful sign-off of `alpha.15` (Entra ID, External Identities, & Hybrid Authentication), the ConvoLab platform has significantly expanded its authoritative state. 

The platform now natively manages:
1. **Security & Access:** Users, Organisations, Workspaces, External identities, Invitations, and Sessions.
2. **Governance & Observability:** Audit events, Analytics outbox, Evaluation data, Policies, and Plugins.
3. **Runtime & Operations:** Configuration state and Operational/Analytics worker leases.

## The Problem
Because ConvoLab now acts as the authoritative source of truth for conversational engineering (prompts, workflows, policies) *and* enterprise identity mapping (Local + Entra Hybrid), any loss of database state represents a critical operational risk. 

The next logical workstream must prove that the platform can recover from total data loss without corrupting:
- **Identity & Access:** ensuring users can still log in and external identity maps remain valid.
- **Audit & Analytics:** ensuring immutable append-only evidence (like outbox events) is not silently dropped or duplicated during recovery.
- **Governance:** ensuring that active policies and plugin configurations remain strictly enforced immediately upon restore.

## Scope for alpha.16

### 1. Data Protection & Snapshotting
- Define the canonical Backup procedure for the PostgreSQL infrastructure.
- Determine the scope of the backup (e.g., full database dump, point-in-time recovery (PITR) configurations).
- Determine how the `ConvoLab` application discriminator and `SharedFileSystem` data protection keys (X.509 PEM files) are backed up to ensure restored session and antiforgery cookies remain valid, or are safely invalidated.

### 2. Recovery Objectives (RPO/RTO)
- Establish explicit Recovery Point Objectives (RPO) and Recovery Time Objectives (RTO).
- Currently, Operations Center reports Backups as `NotConfigured`. `alpha.16` must define and measure this.

### 3. Operational Evidence
- Update the `Operations Center` backend and frontend to actively monitor and report backup health, age, and verification status.
- Transition the `Backups` operational dependency evidence from `NotConfigured` to `Configured` or `LiveValidated`.

### 4. Integrity Verification & Disaster Recovery Rehearsal
- Implement an automated or documented DR rehearsal runbook.
- Ensure that restoring an older snapshot does not trigger duplicate analytics outbox dispatches (requires validating PostgreSQL sequence resets and fencing tokens).
- Verify that break-glass accounts and temporary lockouts behave predictably upon restore.

## Rationale
Moving to `alpha.16 — Backup, Restore & Disaster Recovery` is the correct strategic priority because:
1. **Maturing the Platform:** You cannot declare "Production Readiness" (Beta/V1) without a tested disaster recovery plan.
2. **Safeguarding alpha.15:** The complex state introduced by Hybrid Authentication and External Identities makes data loss catastrophic if users lose their linked OIDC mappings.
3. **Foundational prerequisite:** Deployment promotion (UAT -> Prod) and supply-chain controls (which are currently deferred) rely on knowing that environments can be safely torn down and restored.
