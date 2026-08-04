# Secret stores and readiness

Secret resolution is asynchronous and cancellation-aware. References route to `env:`, `docker-secret:`, or `azure-key-vault:` providers. Successful resolutions alone are cached by canonical reference for 300 seconds by default (maximum 3600); failures are not cached. Updates invalidate both old and new references, while validation and disable operations invalidate the current reference.

Docker secret names reject absolute paths, separators, traversal, containment escape, symlinks/reparse points, and unsafe Unix write permissions. The provider returns safe codes and never serializes or stringifies a resolved value.

Azure Key Vault requires an exact vault-origin allowlist, a bounded timeout, and a controlled retry limit. Development uses the developer-aware default Azure credential chain. UAT and Production instantiate only `WorkloadIdentityCredential` followed by `ManagedIdentityCredential`, optionally with a managed-identity client ID; developer, CLI, PowerShell, interactive, shared-cache, broker, and client-secret fallback credentials are excluded.

Automated Key Vault acceptance uses an injectable deterministic client and reports successful test I/O as `StubValidated`, never `LiveValidated`. Production SDK I/O is the only path that records live validation.

## Effective required-secret scope

Readiness starts with active organisations, active workspaces, and active environments. Development validates the active/default Development environment. UAT and Production use typed `Operations:RequiredSecrets` allowlists of required environment IDs or names and validate that each entry exists in the active owned scope.

For each selected environment the runtime effective-configuration resolver determines whether provider execution is enabled, which provider is effective, whether it is external and requires a secret, and which secret reference wins precedence. Archived environments, disabled providers, overridden raw rows, and providers without secrets do not create false failures. Evidence exposes only environment ID/name, provider, reference scheme, required flag, dependency state, safe failure code, and validation time.

