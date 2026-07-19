# Docker

Run the complete development platform:

```bash
docker compose up --build
```

Endpoints:

- Studio: `http://localhost:3000`
- API: `http://localhost:5000`
- Swagger in Development: `http://localhost:5000/swagger`
- Readiness: `http://localhost:5000/health/ready`

Named volumes preserve PostgreSQL data and uploaded knowledge documents. `docker compose down -v` intentionally deletes both.
