# Error Handling

Application failures use typed `ConvoLabException` subclasses with stable error codes. API middleware maps them to RFC 7807 `application/problem+json` responses containing:

- `status`
- `title`
- `detail`
- `code`
- `correlationId`
- optional field-level `errors`

Unexpected failures return a generic message and correlation ID; stack traces and provider response bodies are never exposed. Studio normalizes Problem Details through `apiClient.ts` and includes correlation IDs in user-facing diagnostics.
