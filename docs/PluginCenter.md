# Plugin Center

Plugin Center is ConvoLab Studio's extension-governance workspace. It makes providers, tools, knowledge connectors, channels, evaluators, trace exporters, workflow nodes, and enterprise integrations discoverable without moving vendor-specific code into Platform Core.

## Purpose

A plugin is a replaceable adapter that implements one or more public ConvoLab capability contracts. Plugin Center stores the adapter's contract and operational evidence; it does not dynamically execute arbitrary uploaded assemblies.

Every registered plugin declares:

- Logical registry key and immutable version
- Name, publisher, category, and description
- Manifest location and optional entry point
- Required Platform API version
- Exposed capability names
- Required permissions
- Configuration schema and metadata
- Lifecycle and health state

## Functional scope in v1

Plugin Center provides:

- Persistent plugin registry
- Immutable version history
- Installed, active, inactive, and deprecated lifecycle
- Provider, tool, knowledge connector, channel, evaluator, trace exporter, workflow-node, and enterprise-connector categories
- Platform API major-version compatibility checks
- Capability and permission declarations
- Configuration-schema validation
- Built-in and HTTP manifest health probes
- Persisted health-check history
- Optimistic revision checks
- One active version per logical plugin, enforced transactionally and by a filtered unique index
- Runtime `IPluginManager` backed by the persistent registry
- Four non-disruptive built-in adapter registrations on an empty database

## Lifecycle

```text
Register -> Installed -> Active -> Inactive
                         |          |
                         +--------> Deprecated
```

Active plugins must be compatible with the current Platform API and must not have an unhealthy probe result. Metadata is editable only while a plugin is not active or deprecated. Creating a new version produces a new Installed record and preserves the previous version as immutable history.

Activating a new version automatically deactivates any previously active version for the same logical plugin. The switch is committed as one repository transaction, and the database prevents two active versions for the same logical plugin.

## Health model

Health is separate from lifecycle:

- `Unknown` — no probe evidence for the current version
- `Healthy` — the built-in adapter or manifest endpoint is available
- `Degraded` — reserved for partial capability health
- `Unhealthy` — the manifest is unsupported, unavailable, timed out, or returned a failure response

Built-in manifests use the `builtin://` scheme and are accepted only when their registry key and manifest URI match a runtime-owned adapter. External manifests are probed using HTTP or HTTPS with a five-second timeout. Redirects, URL credentials, localhost names, and private, local, link-local, unspecified, or unsafe DNS results are rejected before a request is sent. Probe results store status, message, duration, source, and timestamp.

Health checks verify availability only. They do not grant trust or execute plugin code. Runtime discovery returns only active plugins whose health is Healthy or Degraded; Unknown and Unhealthy registrations remain unavailable to Platform Core.

## Compatibility

Plugin Center v1 compares the major version of `PlatformApiVersion` with the current public contract version (`1.0`). An incompatible plugin may be registered and inspected, but it cannot be activated.

This deliberately simple rule gives ConvoLab a stable compatibility boundary before a full SDK package and contract-negotiation protocol are introduced.

## Baseline plugins

On an empty database, Plugin Center registers and activates:

- ConvoLab Deterministic Provider
- Local Knowledge Connector
- Evaluation Metrics Pack
- Persistent Trace Exporter

These registrations describe adapters already compiled into the application. They do not duplicate or replace their existing Infrastructure implementations.

## Persistence

Migration `202607220005_PluginStudioV1` creates:

- `Plugins`
- `PluginHealthChecks`

`Plugins.PluginKey + Version` is unique. A filtered unique index on `PluginKey` applies while `Status = Active`, preventing split-brain activation. Health checks cascade with the plugin version they describe.

## API

```text
GET    /api/plugins/overview
GET    /api/plugins
GET    /api/plugins/{pluginId}
POST   /api/plugins
PUT    /api/plugins/{pluginId}
POST   /api/plugins/{pluginId}/versions
POST   /api/plugins/{pluginId}/health
POST   /api/plugins/{pluginId}/activate
POST   /api/plugins/{pluginId}/deactivate
POST   /api/plugins/{pluginId}/deprecate
```

## Security boundary

Plugin Center v1 is a registry and governance layer. It intentionally does not:

- Upload or load arbitrary DLLs
- Execute remote manifest code
- Install operating-system packages
- Store plugin secrets
- Grant permissions automatically
- Treat a successful health check as a security approval

Future plugin execution must use signed packages, isolated loading, explicit permission approval, secret references, policy checks, and auditable invocation contracts.

## Planned extensions

- Versioned public .NET, TypeScript, and Python SDKs
- Signed plugin packages and publisher verification
- Isolated worker or process execution
- Configuration instances and secret references
- Capability invocation history
- Policy-based permission approval
- Marketplace and private enterprise catalogues
- Upgrade compatibility reports
- Plugin-level traces, cost, and evaluation telemetry
