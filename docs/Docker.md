# Docker & Container Orchestration

> Guide to running, configuring, and maintaining the ConvoLab multi-container stack with Docker and Docker Compose.

---

## 1. Quick Start

Run the entire platform stack locally with a single command:

```bash
# From repository root
docker compose up --build
```

To run in the background (detached mode):

```bash
docker compose up -d --build
```

To stop all services and containers:

```bash
docker compose down
```

> [!WARNING]
> Running `docker compose down -v` will delete named volumes, wiping local PostgreSQL data and uploaded knowledge documents.

---

## 2. Service Architecture

The standard Docker Compose configuration (`docker-compose.yml`) coordinates three interconnected services:

| Service | Container Name | Host Port | Internal Port | Technology | Health Probe |
|---|---|---|---|---|---|
| **api** | `convolab-api` | `5000` | `8080` | ASP.NET Core (.NET 8) | `/health/ready` |
| **web** | `convolab-web` | `3000` | `80` | React 19 + Nginx Alpine | Nginx HTTP status |
| **db** | `convolab-postgres` | `5432` | `5432` | PostgreSQL 16 Alpine | `pg_isready -U postgres` |

---

## 3. Endpoints & Interfaces

Once the stack is running:

- **ConvoLab Studio (UI):** [http://localhost:3000](http://localhost:3000)
- **Platform Core API:** [http://localhost:5000](http://localhost:5000)
- **Swagger / OpenAPI (Development):** [http://localhost:5000/swagger](http://localhost:5000/swagger)
- **Health Live Probe:** [http://localhost:5000/health/live](http://localhost:5000/health/live)
- **Health Readiness Probe:** [http://localhost:5000/health/ready](http://localhost:5000/health/ready)
- **Platform Status API:** [http://localhost:5000/api/platform/status](http://localhost:5000/api/platform/status)

---

## 4. Persistent Volumes & Data

Docker Compose configures dedicated named volumes:

- `postgres_data`: Persists relational database storage, migration history, audit chains, and analytical event records.
- `knowledge_data`: Persists ingested document files, text chunks, and embedding indices.

---

## 5. Configuration & Environment Variables

Copy `.env.example` to `.env` before building:

```bash
cp .env.example .env
```

Key environment variables:

| Variable | Description | Default |
|---|---|---|
| `POSTGRES_DB` | PostgreSQL database name | `convolab` |
| `POSTGRES_USER` | PostgreSQL user | `postgres` |
| `POSTGRES_PASSWORD` | PostgreSQL password | (set in `.env`) |
| `BACKUP_ENCRYPTION_KEY` | 32-byte Base64 key for AES-256-GCM backups | (generate via `openssl rand -base64 32`) |
| `ASPNETCORE_ENVIRONMENT` | Host environment profile | `Development` |
| `CONNECTORS_INFOBIP_WEBHOOKSECRET`| Secret for validating inbound Infobip webhooks | `convolab-infobip-dev-secret-alpha2026` |

---

## 6. Recovery Stack

For automated disaster recovery validation or staging in an isolated environment, use the recovery compose stack:

```bash
docker compose -f docker-compose.recovery.yml up --build
```
