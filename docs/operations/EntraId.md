# Microsoft Entra ID setup

Register a single-tenant web application in Microsoft Entra ID. Configure the platform's exact HTTPS callback URI ending in `/signin-oidc` and signed-out callback ending in `/signout-callback-oidc`. Do not use `common` or `organizations`. ConvoLab requests only `openid`, `profile`, and `email`; Microsoft Graph application permissions and group/role claims are not required.

Use a tenant-specific v2 authority:

```text
https://login.microsoftonline.com/<tenant-guid>/v2.0
```

Store client authentication outside configuration and reference it through one of the supported providers:

```text
env:CONVOLAB_ENTRA_CLIENT_SECRET
docker-secret:convolab-entra-client-secret
azure-key-vault:https://example-vault.vault.azure.net/secrets/convolab-entra-client-secret
```

The secret is resolved asynchronously when the authorization code is redeemed. It is never returned by authentication or Operations APIs.

## Linking an approved user

1. Ensure the ConvoLab user and intended workspace membership exist.
2. A Platform Administrator calls `POST /api/platform/users/{userId}/external-identities/invitations`.
3. Deliver the returned token through an approved confidential channel; the plaintext value is returned once and stored only as a SHA-256 hash.
4. The browser posts it to `/api/auth/entra/prepare-invitation` with the antiforgery token, then starts `/api/auth/entra/login`.
5. ConvoLab creates the identity, consumes the invitation, persists the opaque application session, and writes audit/outbox evidence in one database transaction. The application cookie is issued only after that transaction commits. Concurrent reuse produces one successful link and one safe rejection.

If the ID token contains a syntactically usable `email` claim, it must match the invited email after trimming and invariant case normalization. A missing or unusable `email` claim does not prevent linking: the valid single-use invitation plus validated tenant, issuer, and subject remain authoritative. `preferred_username` and `upn` are profile metadata only and never invitation-email evidence. `email_verified` is not consulted. Email never locates an account or becomes standalone linking authority. Issuer and subject are never returned by broad Operations APIs.

## Validation evidence

Framework OIDC validation covers signature, issuer, audience, expiry, state, nonce, correlation, and authorization response. Deterministic test evidence must be labelled `StubValidated`. Only a successful exchange with the configured Microsoft tenant may set `LiveValidated`. The API does not contact Entra on every readiness request; it exposes cached evidence from authentication activity.

Troubleshoot safe failure codes through structured audit/telemetry and correlation IDs. Never log callback query values or enable identity-model personally identifiable information logging.
