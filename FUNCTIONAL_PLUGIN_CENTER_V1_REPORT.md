# Functional Plugin Center v1 — Integrated Implementation Report

**Release:** `1.0.0-alpha.10`
**Integrated:** 22 July 2026

## Outcome

Plugin Center is now a persistent extension-governance capability rather than a placeholder manager and generic Studio page. It records contracts for adapters already compiled into ConvoLab or hosted behind external manifests without uploading or executing arbitrary plugin assemblies.

## Delivered

- Logical plugin registry keys with immutable semantic versions and one transactional active version.
- Provider, tool, knowledge connector, channel, evaluator, trace exporter, workflow node, and enterprise connector categories.
- Installed, Active, Inactive, and Deprecated lifecycle with active/deprecated immutability.
- Platform API major-version compatibility enforcement.
- Capability, permission, entry-point, configuration-schema, and metadata contracts.
- Separate Unknown, Healthy, Degraded, and Unhealthy operational health with persisted probe history.
- Runtime discovery limited to active plugins with healthy or degraded evidence.
- Four idempotent-on-empty built-in registrations for the deterministic provider, local knowledge connector, evaluation metrics, and persistent tracing adapters.
- A modern `/plugins` workspace with responsive registry cards, filters, contract inspection, version history, structured forms, success/error/loading/empty states, and guarded deprecation confirmation.
- Functional `/documentation/plugins` guidance and complete `/api/plugins/*` contracts.

## Security boundary

- Arbitrary DLL upload, dynamic assembly loading, package installation, permission granting, and secret storage remain disabled.
- Only the four runtime-owned `builtin://` key/manifest pairs are trusted as built-ins.
- HTTP health probes reject credentials in URLs, localhost, local/private/link-local/unspecified addresses, unsafe DNS results, and redirects.
- Health success proves availability only; it does not grant trust or execute manifest code.

## Persistence upgrade

Migration `202607220005_PluginStudioV1` adds `Plugins` and `PluginHealthChecks`, unique logical-version identity, optimistic revisions, health indexes, and a filtered unique active-version index. Activation deactivates the previous version and activates its successor in one transaction; a failed successor write rolls back the previous deactivation.

The archive's SQLite `DateTimeOffset` ordering failure was corrected with provider-aware query ordering. The controller lifecycle route also avoids ASP.NET's reserved `action` route token.

## Validation

- .NET 8 Release build passed; all 201 solution tests passed.
- Domain/application coverage verifies health-gated activation, active immutability, successor history, atomic active-version switching, and compatibility rejection.
- Infrastructure coverage verifies fresh SQLite migration, complete schema, private/built-in probe safety, and activation rollback.
- API coverage verifies registration, persisted health, safe activation failure, immutable successor creation, RFC 7807 responses, and stable v1 metadata.
- Frontend lint, production build, contract tests, and the 24-file interaction audit passed.
- The 11-route desktop/responsive browser smoke suite passed, including Plugin Center documentation, filters, editor cancellation, health, version, and deprecation-confirmation interactions.
- Docker Compose rebuilt successfully against PostgreSQL. Existing Evaluation, Trace, Replay, and Policy identifiers were preserved, the four built-ins were seeded, and plugin health history survived database/API restart.

## V1 boundaries

- Plugin Center governs registration and discovery; it does not dynamically invoke arbitrary extension code.
- Compatibility is based on Platform API major version rather than full contract negotiation.
- Signed packages, publisher verification, isolated workers, configuration instances, secret references, permission approval, marketplace distribution, and invocation telemetry remain future work.
