# Release Engineering & Status Maintenance

This document outlines the guidelines for maintaining status badges and handling releases in ConvoLab Studio.

## Capability & Feature Status

Every feature, page, and underlying capability in ConvoLab Studio has a `status` property defined in `web/src/data/platform.ts`. These statuses appear as coloured pills throughout the application, including the main navigation sidebar and the dashboard's capability map.

It is **critical** that these statuses accurately reflect the state of the system for any given deployment.

### Definitions

- **`stable` (Green)**: The feature is fully implemented, thoroughly tested, and production-ready. It has no known major bugs, and its API contracts are not expected to have breaking changes without version bumps.
- **`active` (Blue)**: The feature is working and accessible, but it may still be evolving. It might be missing secondary capabilities or its API might change. Use this for new features that are "live" but not yet complete.
- **`foundation` (Purple)**: The feature is scaffolded or partially implemented, but it is not yet user-facing complete. It may be missing a UI, or the UI may be incomplete.
- **`planned` (Grey)**: The feature is on the roadmap but development has not yet begun.

## Maintenance Checklist for Releases

Before cutting a new release (or pushing major updates), the development team must:

1. **Review `platform.ts`**: Check every entry in `capabilities` and `navigationItems`.
2. **Update Statuses**: If a feature has transitioned from `foundation` to `active`, or `active` to `stable`, update the string value.
3. **Verify Dashboard**: Ensure the capability map on the Dashboard accurately represents the new status.

Failure to maintain these statuses will result in an inaccurate dashboard and confusing navigation for users.

---

## Release Notes Index

- **Current Release Candidate:** [`v1.0.0-enterprise`](docs/releases/PlatformCore-v1.0.0-enterprise.md) — Four strategic enterprise pillars delivered (PII redaction, tamper-evident audit trail, omnichannel Infobip/WhatsApp connector, ALM environment promotion & rollback, Executive FinOps in ZAR).
- **Historical Milestones & Release Notes:** [docs/releases/](docs/releases/)
  - `v1.0.0-alpha.19`: [PlatformCore-v1.0.0-alpha.19.md](docs/releases/PlatformCore-v1.0.0-alpha.19.md) (Live environment validation, load testing, automated DR drill)
  - `v1.0.0-alpha.18`: [PlatformCore-v1.0.0-alpha.18.md](docs/releases/PlatformCore-v1.0.0-alpha.18.md) (Security & compliance hardening)
  - `v1.0.0-alpha.17`: [PlatformCore-v1.0.0-alpha.17.md](docs/releases/PlatformCore-v1.0.0-alpha.17.md) (Deployment, environment promotion, release engineering)
  - `v1.0.0-alpha.16`: [PlatformCore-v1.0.0-alpha.16.md](docs/releases/PlatformCore-v1.0.0-alpha.16.md) (Backup, restore & disaster recovery)
  - `v1.0.0-alpha.15`: [PlatformCore-v1.0.0-alpha.15.md](docs/releases/PlatformCore-v1.0.0-alpha.15.md) (Microsoft Entra ID & hybrid auth)
