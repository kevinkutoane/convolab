# Authentication incident response

For suspected identity compromise, first disable the external identity through the Platform Administrator API. This immediately revokes its active ConvoLab sessions. Preserve `Authentication.ExternalIdentityDisabled` and `Authentication.SessionRevokedForIdentityDisablement` audit evidence, then coordinate Entra account/session revocation with the tenant operator.

For Entra outage, existing ConvoLab sessions continue until their idle or absolute expiry. Hybrid installations may use approved local access; Entra-only installations use break glass only when it was explicitly provisioned before the incident. Never enable a new emergency credential during an unreviewed incident.

For invitation leakage, revoke/replace the active invitation. Tokens are single-use and stored hashed; do not paste them into tickets, chat, logs, Analytics, or telemetry. For unexpected tenant/issuer/audience failures, verify the tenant-specific authority and application registration rather than weakening validation.

For suspected token or secret leakage, rotate the Entra client secret at its provider, invalidate its secret-store cache by restarting the API, inspect sanitized audit and telemetry, and search evidence stores for the sentinel or leaked value. Do not attach raw callback URLs or tokens to incident records.

After containment, document time, mode, dependency state, affected identity/session references, revocation outcome, break-glass use, and whether live Entra validation was rerun. Keep live and stub evidence clearly separated.
