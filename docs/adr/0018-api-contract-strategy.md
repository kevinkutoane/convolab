# ADR 0018: API contract strategy

**Status:** Accepted for alpha

OpenAPI emitted by the ASP.NET Core API is the authoritative HTTP description. Studio keeps explicit strict TypeScript contracts and validates normalized Problem Details through contract tests. Generated TypeScript clients are the planned replacement before public SDK release.
