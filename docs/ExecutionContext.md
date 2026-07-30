# Trusted execution context

Every environment-aware operation uses the scoped runtime request context:

```text
OrganisationId
WorkspaceId
EnvironmentId
ActorId
ActorType
ActorRole
CorrelationId
ParentRequestId
EnvironmentResolutionMode
```

The server creates the authoritative correlation ID. A client correlation value is retained only as parent-request metadata. The resolved environment and correlation are returned in response headers.

`X-ConvoLab-Environment-Id` selects an explicit environment. When omitted, the active default environment is resolved for backward-compatible Alpha 13 calls. Selection is validated against the authenticated organisation and workspace before it enters the request context.

Errors use consistent Problem Details:

| Condition | Status | Code |
| --- | ---: | --- |
| Malformed environment identifier | 400 | `runtime_environment.invalid` |
| Unknown or foreign environment | 404 | `environment.not_found` |
| Inactive environment | 409 | `environment.inactive` |
| Default unavailable | 409 | `environment.default_unavailable` |

## Runtime configuration

The runtime resolves typed effective settings at organisation, workspace, and environment scope. Execution overrides such as simulator provider, model, temperature, and maximum output tokens are validated before use.

The complete non-secret effective configuration plus validated overrides is canonicalised, sorted, and hashed with SHA-256. The immutable snapshot is persisted before execution. Provenance is stored separately and does not affect the content revision. Secret references may appear in provenance; secret values never do.

Simulation, policy, provider, evaluation, trace, replay, attribution, and analytics records share the actual configuration revision and correlation. Replay uses the original snapshot by default; explicit current-configuration replay records the new revision and difference.
