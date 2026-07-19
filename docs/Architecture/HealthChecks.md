# Health Checks

- `GET /health/live` confirms the API process is alive.
- `GET /health/ready` checks database connectivity/migration state, document-storage writability, and provider configuration.
- `GET /health` is a readiness-compatible alias.
- `GET /api/platform/status` describes product and capability state.

A missing Gemini key yields **Degraded**, not Unhealthy, because the deterministic provider remains available. External Gemini connectivity is tested explicitly through the provider test endpoint and is not called by routine readiness probes.
