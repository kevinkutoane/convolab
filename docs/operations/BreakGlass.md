# Break-glass access runbook

Break glass is disabled by default and is available only in `Entra` or `Hybrid` mode. It uses the existing PBKDF2 local credential for an active Platform Administrator; no default credential is shipped.

Before enabling it:

1. Provision a dedicated Platform Administrator with a unique, strong local password through the approved identity process.
2. Verify the account is active and has a `LocalCredentials` record.
3. Protect the credential in the organisation's emergency-access vault and record dual-control ownership.
4. Set `Authentication:Local:BreakGlassAccountConfigured=true`, then `BreakGlassEnabled=true` through protected operational configuration.
5. Restart and confirm the Operations Authentication panel reports the expected state.

The typed defaults are five attempts, a 15-minute break-glass-only lockout, and three endpoint requests per minute. `Authentication:Local:LoginRateLimitPerMinute` independently defaults to 10. Production validation accepts ordinary rates from 1–100, break-glass attempts from 3–10, lockout duration from 1–1440 minutes, and break-glass rates from 1–60.

Operators use the deliberately separate **Emergency administrator access** action. Ordinary local login remains hidden in Entra mode. Failed attempts affect only the dedicated break-glass counter and lock; ordinary local `FailedAttempts` and `LockedUntil` are unchanged. The response is always `authentication.break_glass_denied` for invalid, unknown, unauthorised, or locked attempts. A lock does not extend when retried, expires deterministically, and a later successful login resets its state.

A successful login creates a one-hour `BreakGlass` application session and emits high-severity `Authentication.BreakGlassLogin` evidence. Every denial emits `Authentication.BreakGlassFailure`. `Authentication.BreakGlassLocked` is emitted once for the transition that reaches the configured threshold; subsequent attempts during that active lockout continue to emit `Authentication.BreakGlassFailure` with `lockoutState=Locked` and do not emit another transition event. `convolab.auth.break_glass.total` uses only bounded `outcome`, `failure_code`, and `lockout_state` labels. Operations exposes only aggregate availability, state, successful-use/failure counts, and last success alongside the sanitized Entra/identity evidence.

After any use, revoke all emergency sessions, rotate the password, review audit and telemetry evidence, establish why SSO was bypassed, and disable the path when the incident ends. Production startup is rejected when break glass is enabled but no active Platform Administrator local credential exists.
