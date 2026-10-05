# ConvoLab roadmap

Current release candidate: `v1.0.0-enterprise — Release Candidate` (four strategic pillars delivered).
Previous formal release: `v1.0.0-alpha.18` (commit `073152a40fe81cb3ea3669eeb512d345f6032a4b`).  
Historical operational milestone: `alpha.19 — Live Environment Validation & Load Testing` (tagged `v1.0.0-alpha.19`, formally closed on `main`).

The delivered workstream is `alpha.19 — Live Environment Validation & Load Testing`: Native Node.js multi-workload load testing harness (Read, Write, Execution, Security workloads) with zero external binary dependencies; endurance/soak runner with rolling 5-second sampling; isolated disaster recovery drill harness and 12-step automated verification; deterministic release artifact verifier (SBOM checksums, immutable digest pinning, provenance); lightweight performance smoke CI gate and dedicated endurance workflow; formal Entra live-tenant acceptance protocol and runner (execution `Blocked (Environment Gate)` — not a code blocker); empirical performance baseline (Read ~245 RPS p95 38.2ms, Write ~112 RPS p95 88.7ms, Execution ~168 RPS p95 59.4ms — all within provisional targets, zero unhandled 500s); DR objective validation (RTO < 15s, RPO 0s).

The previously delivered workstream is `alpha.18 — Security & Compliance Hardening` (`v1.0.0-alpha.18`): Hardened HTTP security headers middleware (COOP, COEP, XCDO, CSP, 2-year HSTS), extended audit trail covering member/identity/environment/settings mutations, dedicated AuditController with paginated export, SensitiveOutputSanitizerMiddleware and SensitiveTelemetryLogFilter, targeted rate-limiting on high-risk surfaces, BlockAnalyticsExports production default fixed, BlockAuditExports introduced, four new ProductionReadinessValidator rules, ThreatModel.md and SOC 2-aligned ComplianceControls.md.

The previously delivered workstream is `alpha.17 — Deployment, Environment Promotion & Release Engineering`: Immutable GHCR container publishing, dual CycloneDX SBOMs, cryptographic build provenance, container vulnerability gates, automated pre-migration backup enforcement, and an audited Environment Promotion control plane inside the Operations Center.

The previously delivered workstream is `alpha.16 — Backup, Restore & Disaster Recovery`: Defining the DR runbook, PostgreSQL point-in-time recovery configurations, data protection key preservation, and Operations Center RPO/RTO telemetry.

The previously delivered workstream is `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication`: Local/Entra/Hybrid modes, explicit issuer-and-subject identity linking, invitation-based first login, opaque ConvoLab sessions, external logout, Platform Administrator identity controls, and exceptional break-glass access.

Live Microsoft Entra tenant validation remains an environment gate (StubValidated). Group-to-role mapping, multi-tenant Entra, SCIM, automatic employee onboarding, and live channel integrations are deferred.

Next planned milestone: `alpha.20 — Environment & Secret Management` (per-environment configuration governance, secret rotation controls, environment promotion rules, and secret-store integration hardening).
