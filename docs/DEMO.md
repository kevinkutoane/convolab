# ConvoLab Demo Setup

1. Clone repository and navigate to root: `git clone <repo-url> && cd convolab`
2. Initialize environment config: `cp .env.example .env` (optionally add `GEMINI_API_KEY`)
3. Launch container stack: `docker compose up --build -d`
4. Verify backend health: `curl -f http://localhost:5000/health/live`
5. Access ConvoLab Studio: `http://localhost:3000` (or API directly at `http://localhost:5000`)
6. Sign in with bootstrap admin: `admin@example.com` / `ReplaceWithStrongPassword123!` (or `.env` values)
7. Teardown demo environment: `docker compose down`
