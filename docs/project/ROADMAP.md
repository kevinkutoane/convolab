# ConvoLab roadmap

Active release metadata is `1.0.0-alpha.18`.

The delivered workstream is `alpha.18 — Security & Compliance Hardening`: Hardened HTTP security headers middleware (COOP, COEP, XCDO, CSP, 2-year HSTS), extended audit trail covering member/identity/environment/settings mutations, dedicated AuditController with paginated export, SensitiveOutputSanitizerMiddleware and SensitiveTelemetryLogFilter, targeted rate-limiting on high-risk surfaces, BlockAnalyticsExports production default fixed, BlockAuditExports introduced, four new ProductionReadinessValidator rules, ThreatModel.md and SOC 2-aligned ComplianceControls.md.

The previously delivered workstream is `alpha.17 — Deployment, Environment Promotion & Release Engineering`: Immutable GHCR container publishing, dual CycloneDX SBOMs, cryptographic build provenance, container vulnerability gates, automated pre-migration backup enforcement, and an audited Environment Promotion control plane inside the Operations Center.

The previously delivered workstream is `alpha.16 — Backup, Restore & Disaster Recovery`: Defining the DR runbook, PostgreSQL point-in-time recovery configurations, data protection key preservation, and Operations Center RPO/RTO telemetry.

The previously delivered workstream is `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication`: Local/Entra/Hybrid modes, explicit issuer-and-subject identity linking, invitation-based first login, opaque ConvoLab sessions, external logout, Platform Administrator identity controls, and exceptional break-glass access.

Live Microsoft Entra tenant validation remains an environment gate (StubValidated). Group-to-role mapping, multi-tenant Entra, SCIM, automatic employee onboarding, and live channel integrations are deferred.
