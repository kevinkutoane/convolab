# Break-glass access runbook

Break glass is disabled by default and is available only in `Entra` or `Hybrid` mode. It uses the existing PBKDF2 local credential for an active Platform Administrator; no default credential is shipped.

Before enabling it:

1. Provision a dedicated Platform Administrator with a unique, strong local password through the approved identity process.
2. Verify the account is active and has a `LocalCredentials` record.
3. Protect the credential in the organisation's emergency-access vault and record dual-control ownership.
4. Set `Authentication:Local:BreakGlassAccountConfigured=true`, then `BreakGlassEnabled=true` through protected operational configuration.
5. Restart and confirm the Operations Authentication panel reports the expected state.

Operators use the deliberately separate **Emergency administrator access** action. Ordinary local login remains hidden in Entra mode. A successful login creates a one-hour `BreakGlass` application session, emits high-severity `Authentication.BreakGlassLogin` audit evidence, the `BreakGlassLogin` Analytics event where an Analytics scope exists, and `convolab.auth.break_glass.total` telemetry.

After any use, revoke all emergency sessions, rotate the password, review audit and telemetry evidence, establish why SSO was bypassed, and disable the path when the incident ends. Production startup is rejected when break glass is enabled but no active Platform Administrator local credential exists.
