# Authentication operations

ConvoLab supports `Local`, `Entra`, and `Hybrid` modes. Microsoft Entra authenticates a person; ConvoLab remains authoritative for Platform Administrator status, organisations, workspaces, memberships, roles, and permissions.

| Mode | Primary sign-in | Ordinary local sign-in | Break glass |
| --- | --- | --- | --- |
| Local | Local credential | Available | Unavailable |
| Entra | Microsoft Entra | Hidden and rejected | Optional, separately enabled |
| Hybrid | Microsoft Entra | Only when policy enables it | Optional, separately enabled |

`GET /api/auth/options` is public and returns only the mode and availability flags. It does not expose authority, tenant, client, callback, or secret-reference details.

An Entra sign-in follows: OIDC authorization code with PKCE → issuer/tenant/audience/signature/lifetime/state/nonce validation → `(Provider, Issuer, Subject)` lookup → ConvoLab user lookup → opaque ConvoLab session. The ID/access tokens are not stored. The application cookie remains HttpOnly and `SameSite=Strict`; only framework OIDC nonce/correlation cookies use `SameSite=None`, `Secure=Always`, and HttpOnly.

Unknown external identities receive `authentication.external_identity_not_linked`. Email never causes an automatic link. A Platform Administrator must create a single-use invitation, and the first callback must contain a verified email matching the invitation after trim plus invariant-uppercase normalization.

Local and Entra sessions use an eight-hour idle expiry and a 24-hour absolute boundary. Break-glass sessions expire after one hour. Logout revokes the database session before any external sign-out attempt, so local revocation remains effective if Entra is unavailable. Return URLs accept only local `/...` paths and reject absolute, protocol-relative, backslash, control-character, and encoded protocol-relative forms.

Operational evidence is available to Platform Administrators at `GET /api/operations/authentication`. Safe mode does not block authentication, callbacks, logout, session validation, identity administration, or break glass.
