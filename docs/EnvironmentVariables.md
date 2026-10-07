# Environment Variables Reference

> Canonical reference for all configuration settings and environment variables supported by ConvoLab Platform Core and ConvoLab Studio.

---

## 1. Core Platform & Bootstrap

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Recommended | `Development` | ASP.NET Core environment mode (`Development`, `Staging`, `Production`, `Testing`). |
| `CONVOLAB_BOOTSTRAP_ADMIN_EMAIL` | Optional | `admin@convolab.test` | Initial administrator account email for seed bootstrap. |
| `CONVOLAB_BOOTSTRAP_ADMIN_NAME` | Optional | `ConvoLab Administrator` | Initial administrator display name. |
| `CONVOLAB_BOOTSTRAP_ADMIN_PASSWORD` | Recommended | `Ephemeral-Alpha12!` | Initial administrator account password. |
| `BACKUP_ENCRYPTION_KEY` | Production | — | 32-byte Base64 encoded key used for AES-256-GCM backup encryption. |

---

## 2. Database & Persistence

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | Yes (in Prod) | `Data Source=convolab.db` (local SQLite) | PostgreSQL or SQLite connection string. |
| `Database__ApplyMigrationsOnStartup` | Development | `false` | Automatic execution of pending EF Core migrations on startup. Must be `false` in Production. |
| `POSTGRES_DB` | Docker only | `convolab` | Relational database name within PostgreSQL container. |
| `POSTGRES_USER` | Docker only | `postgres` | Database administrator username within container. |
| `POSTGRES_PASSWORD` | Docker only | — | Database administrator password within container. |

---

## 3. AI Providers & FinOps (ZAR)

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `GEMINI_API_KEY` | If using Gemini | — | Google AI Studio or Vertex AI Gemini API key. |
| `GEMINI_MODEL` | Optional | `gemini-2.5-flash` | Default Gemini model identifier for intelligence executions. |
| `CONVOLAB_MONTHLY_AI_BUDGET_ZAR` | Optional | `500.00` | Monthly tenant budget limit in South African Rand (ZAR) for FinOps guardrails. |
| `GEMINI_INPUT_PRICE_ZAR_PER_1K` | Optional | `0.0013` | Configured input token cost rate per 1,000 tokens in ZAR. |
| `GEMINI_OUTPUT_PRICE_ZAR_PER_1K` | Optional | `0.0052` | Configured output token cost rate per 1,000 tokens in ZAR. |

---

## 4. Evaluation Studio Quality Gates

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `CONVOLAB_EVALUATION_MIN_GROUNDEDNESS` | Optional | `0.80` | Minimum acceptable groundedness score (0.0 to 1.0) before triggering failure. |
| `CONVOLAB_EVALUATION_MIN_RELEVANCE` | Optional | `0.80` | Minimum acceptable semantic relevance score (0.0 to 1.0). |
| `CONVOLAB_EVALUATION_MIN_SAFETY` | Optional | `0.95` | Minimum acceptable safety threshold (0.0 to 1.0). |
| `CONVOLAB_EVALUATION_MIN_OVERALL` | Optional | `0.82` | Minimum composite score required for release promotion approval. |
| `CONVOLAB_EVALUATION_FAILURE_ACTION` | Optional | `Review` | Policy action when scorecard fails (`Warn`, `Review`, `Deny`). |

---

## 5. Omnichannel Connectors (Infobip & WhatsApp)

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `Connectors__Infobip__WebhookSecret` | Inbound Hook | `convolab-infobip-dev-secret-alpha2026` | Shared secret for verifying Infobip HMAC-SHA256 webhook signatures and headers. |
| `Connectors__Infobip__ApiKey` | For outbound | — | Infobip API key for outbound WhatsApp message delivery. |
| `Connectors__Infobip__BaseUrl` | For outbound | `https://api.infobip.com` | Base URL for Infobip REST API communication. |

---

## 6. Frontend / Studio Variables

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `VITE_API_BASE_URL` | Optional | `/api` | Base URL for API requests made by the React Studio frontend. |
| `NODE_ENV` | Build time | `production` | Node.js runtime environment mode. |

---

> [!SECURITY]
> Secrets and production keys (`BACKUP_ENCRYPTION_KEY`, `GEMINI_API_KEY`, `POSTGRES_PASSWORD`) must be injected through environment variables, cloud secrets vaults (Azure Key Vault, AWS Secrets Manager), or Docker secrets. **Never commit real secrets to source control or expose them to the browser client.**
