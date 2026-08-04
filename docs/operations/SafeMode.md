# Safe mode

Effective safe mode is the logical OR of persisted `PlatformOperationalSettings` state and `CONVOLAB_SAFE_MODE=true`. The environment override takes precedence and cannot be deactivated through the API.

When active, the backend always blocks external providers, provider validation, plugin activation, outbound plugin probes, and external replay execution with `503` Problem Details code `operations.safe_mode_active`. Deterministic simulation/replay is allowed only when `SafeMode:AllowDeterministicVerification=true`. Analytics exports follow the explicit nullable `SafeMode:BlockAnalyticsExports` decision; Production startup fails when that value is unspecified.

Activation and deactivation require expected revision, a meaningful reason, and the exact confirmation text. A successful mutation transactionally persists state and audit evidence, enqueues trusted Analytics evidence when a default active evidence scope exists, and emits warning-level structured logging plus safe telemetry. Stale revisions return concurrency Problem Details.

Studio refreshes platform status every 45 seconds, on focus, when visibility returns, after authentication/workspace restoration, after safe-mode mutations, and after `operations.safe_mode_active`. Refresh failures preserve the last known active banner and mark it stale; the browser is never the enforcement boundary.

