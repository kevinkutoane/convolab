# Environment Variables

| Variable | Required | Purpose |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Docker provides it | PostgreSQL or SQLite connection string |
| `Knowledge__StoragePath` | Optional | Document storage root |
| `Database__ApplyMigrationsOnStartup` | Optional | Development-only automatic migrations |
| `GEMINI_API_KEY` | Only for Gemini | Server-side Gemini credential |
| `GEMINI_MODEL` | Optional | Defaults to `gemini-2.5-flash` |
| `VITE_API_BASE_URL` | Optional | Studio API base URL outside the default proxy topology |

Secrets must be injected through environment or a secret manager and must never be committed or sent to Studio.
