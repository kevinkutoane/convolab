# Deployment Guide

This document provides comprehensive instructions for deploying the ConvoLab application to various environments.

## Table of Contents

1. [Local Development](#local-development)
2. [Docker Deployment](#docker-deployment)
3. [Production Deployment](#production-deployment)
4. [Environment Configuration](#environment-configuration)
5. [Database Migration](#database-migration)
6. [Monitoring and Logging](#monitoring-and-logging)

## Local Development

### Prerequisites

- .NET 10 SDK
- Node.js 18+
- Docker & Docker Compose (optional)

### Setup

#### Backend

```bash
# Navigate to project root
cd /home/ubuntu/convolab

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run migrations (if needed)
dotnet ef database update \
  --project src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj \
  --startup-project src/Api/ConvoLab.Api/ConvoLab.Api.csproj

# Start API
dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

The API will be available at `http://localhost:5000`

#### Frontend

```bash
# Navigate to frontend directory
cd web

# Install dependencies
npm install

# Start development server
npm run dev
```

The frontend will be available at `http://localhost:3000`

### Using Docker Compose

```bash
# Start all services
docker-compose up

# Stop services
docker-compose down

# View logs
docker-compose logs -f

# Rebuild images
docker-compose up --build
```

## Docker Deployment

### Building Docker Image

```bash
# Build image
docker build -t convolab:latest .

# Tag for registry
docker tag convolab:latest myregistry.azurecr.io/convolab:latest

# Push to registry
docker push myregistry.azurecr.io/convolab:latest
```

### Running Docker Container

```bash
# Run with SQLite (development)
docker run -p 5000:8080 convolab:latest

# Run with PostgreSQL (production)
docker run \
  -p 5000:8080 \
  -e "ConnectionStrings__DefaultConnection=Server=postgres;Database=convolab;User Id=convolab_user;Password=convolab_password;" \
  -e "ASPNETCORE_ENVIRONMENT=Production" \
  convolab:latest
```

### Docker Compose for Production

Create `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: convolab
      POSTGRES_USER: convolab_user
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    networks:
      - convolab-network

  api:
    image: myregistry.azurecr.io/convolab:latest
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Server=postgres;Database=convolab;User Id=convolab_user;Password=${DB_PASSWORD};"
    ports:
      - "5000:8080"
    depends_on:
      - postgres
    networks:
      - convolab-network

volumes:
  postgres_data:

networks:
  convolab-network:
```

Deploy:

```bash
docker-compose -f docker-compose.prod.yml up -d
```

## Production Deployment

### Prerequisites

- Production-grade PostgreSQL database
- SSL/TLS certificate
- Reverse proxy (nginx, Apache, etc.)
- Monitoring and logging infrastructure

### Backend Deployment

#### 1. Build Release

```bash
# Build release configuration
dotnet build -c Release

# Publish
dotnet publish -c Release -o ./publish
```

#### 2. Configure Environment

Set environment variables on the production server:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Server=prod-db.example.com;Database=convolab;User Id=convolab_user;Password=secure_password;"
export Jwt__SecretKey="your-production-secret-key"
```

#### 3. Run Application

```bash
# Using systemd service
sudo systemctl start convolab

# Or manually
./publish/ConvoLab.Api
```

#### 4. Configure Reverse Proxy (nginx)

```nginx
upstream convolab_backend {
    server localhost:5000;
}

server {
    listen 80;
    server_name api.convolab.com;
    
    # Redirect HTTP to HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.convolab.com;

    ssl_certificate /etc/ssl/certs/convolab.crt;
    ssl_certificate_key /etc/ssl/private/convolab.key;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;

    location / {
        proxy_pass http://convolab_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### Frontend Deployment

#### 1. Build Production Bundle

```bash
cd web
npm run build
```

#### 2. Deploy to Static Hosting

**Option A: Vercel**

```bash
npm install -g vercel
vercel
```

**Option B: Netlify**

```bash
npm install -g netlify-cli
netlify deploy --prod --dir=dist
```

**Option C: AWS S3 + CloudFront**

```bash
aws s3 sync dist/ s3://convolab-frontend/
aws cloudfront create-invalidation --distribution-id DISTRIBUTION_ID --paths "/*"
```

**Option D: nginx**

```bash
# Copy dist to web root
sudo cp -r dist/* /var/www/convolab/

# Configure nginx
sudo systemctl restart nginx
```

## Environment Configuration

### Production Environment Variables

Create `.env.production` or set system environment variables:

```bash
# API Configuration
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Database
ConnectionStrings__DefaultConnection=Server=prod-db.example.com;Database=convolab;User Id=convolab_user;Password=secure_password;

# Security
Jwt__SecretKey=your-production-secret-key-change-this
Jwt__ExpirationMinutes=60

# Logging
Serilog__MinimumLevel=Warning

# CORS
AllowedHosts=api.convolab.com,*.convolab.com
```

### Secrets Management

Use a secrets manager for sensitive data:

**Azure Key Vault:**

```bash
az keyvault secret set --vault-name convolab-kv --name "ConnectionStrings--DefaultConnection" --value "connection-string"
```

**AWS Secrets Manager:**

```bash
aws secretsmanager create-secret \
  --name convolab/db-password \
  --secret-string "secure-password"
```

**HashiCorp Vault:**

```bash
vault kv put secret/convolab/database \
  connection_string="connection-string"
```

## Database Migration

### Pre-Deployment

1. **Backup Production Database**

```bash
# PostgreSQL backup
pg_dump -h prod-db.example.com -U convolab_user -d convolab > backup.sql

# Restore if needed
psql -h prod-db.example.com -U convolab_user -d convolab < backup.sql
```

2. **Test Migrations Locally**

```bash
# Create test database
dotnet ef database drop --force
dotnet ef database update
```

3. **Create Migration Script**

```bash
dotnet ef migrations script -o migration.sql
```

### Deployment

```bash
# Apply migrations
dotnet ef database update \
  --project src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj \
  --startup-project src/Api/ConvoLab.Api/ConvoLab.Api.csproj

# Or using migration script
psql -h prod-db.example.com -U convolab_user -d convolab -f migration.sql
```

## Monitoring and Logging

### Serilog Configuration

Production logging is configured in `appsettings.Production.json`:

```json
{
  "Serilog": {
    "MinimumLevel": "Warning",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/convolab/convolab-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq.example.com:5341"
        }
      }
    ]
  }
}
```

### Health Checks

Monitor application health:

```bash
# Check API health
curl https://api.convolab.com/health

# Expected response
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy"
  }
}
```

### Distributed Tracing

OpenTelemetry is configured for distributed tracing. Configure exporters:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://otel-collector:4317");
        }));
```

### Monitoring Tools

**Prometheus Metrics:**

```bash
# Expose metrics endpoint
GET /metrics
```

**Application Insights:**

```csharp
builder.Services.AddApplicationInsightsTelemetry(configuration);
```

**Datadog:**

```bash
# Install Datadog agent
DD_AGENT_HOST=localhost DD_TRACE_ENABLED=true dotnet ConvoLab.Api.dll
```

## Scaling Considerations

### Horizontal Scaling

For multiple instances:

1. **Load Balancer Configuration**

```nginx
upstream convolab_api {
    server api1.example.com:5000;
    server api2.example.com:5000;
    server api3.example.com:5000;
    
    # Enable health checks
    check interval=3000 rise=2 fall=5 timeout=1000 type=http;
    check_http_send "GET /health HTTP/1.0\r\n\r\n";
    check_http_expect_alive http_2xx;
}
```

2. **Session Management**

Use distributed cache (Redis) for session state:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});
```

3. **Database Connection Pooling**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Max Pool Size=100;"
  }
}
```

### Vertical Scaling

Increase server resources:

- CPU cores
- Memory (RAM)
- Disk I/O
- Network bandwidth

## Rollback Procedure

If deployment fails:

```bash
# Revert to previous version
docker pull myregistry.azurecr.io/convolab:previous
docker-compose -f docker-compose.prod.yml down
docker-compose -f docker-compose.prod.yml up -d

# Or restore from backup
psql -h prod-db.example.com -U convolab_user -d convolab < backup.sql
```

## Security Checklist

- [ ] Enable HTTPS/TLS
- [ ] Configure firewall rules
- [ ] Set strong database passwords
- [ ] Enable database backups
- [ ] Configure rate limiting
- [ ] Enable CORS appropriately
- [ ] Set secure headers
- [ ] Enable logging and monitoring
- [ ] Regular security updates
- [ ] Database encryption at rest
- [ ] Secrets stored securely
- [ ] API authentication enabled

## Performance Optimization

1. **Database Indexing**

```sql
CREATE INDEX idx_user_email ON users(email);
CREATE INDEX idx_conversation_user_id ON conversations(user_id);
```

2. **Caching Strategy**

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});
```

3. **CDN for Static Assets**

```html
<script src="https://cdn.example.com/app.js"></script>
<link rel="stylesheet" href="https://cdn.example.com/app.css">
```

4. **Compression**

```nginx
gzip on;
gzip_types text/plain text/css application/json application/javascript;
gzip_min_length 1000;
```

## Related Documentation

- See `README.md` for quick start
- See `ARCHITECTURE.md` for architecture overview
- See `.github/workflows/ci.yml` for CI/CD pipeline
