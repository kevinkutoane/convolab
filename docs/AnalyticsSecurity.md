# Analytics security and privacy

Analytics is workspace-scoped and server-authorised. There is no organisation-wide or cross-workspace rollup in Alpha 14.

Every query validates the authenticated organisation, workspace, and requested environment. Foreign workspace, organisation, environment, event, and correlation identifiers are indistinguishable from missing resources and return `404`.

## Field visibility

The API resolves field visibility independently of route access:

- actor permission controls actor ID, role/type detail, and sensitive source references;
- cost permission controls ZAR values, cost type, pricing revision, and token usage;
- environment permission controls environment-level provider/model detail;
- Engineer Production restrictions override otherwise granted cost permission.

The same visibility snapshot is stored with an export request, preventing the asynchronous worker from widening access later. Frontend hiding is never an authorisation boundary.

## Data boundary

Analytics persistence and CSV files exclude raw prompt and message content, trace payloads, provider bodies, credentials, tokens, and secret values. Configuration snapshots include only non-secret effective values; secret provenance stores references only.

CSV cells beginning with spreadsheet formula characters are escaped. Exports are audited on creation and download, capped at 100,000 rows/25 MB, and expire under governed retention.

Security acceptance covers unauthenticated access, tenant isolation, guessed IDs, role and actor redaction, Production cost/event/correlation bypass attempts, export visibility, formula injection, and sensitive-data scans.
