# ConvoLab Architecture Handbook v1

This handbook is the architecture baseline for Platform Core and ConvoLab Studio `v1.0.0-alpha.18`.

## Contents

- [Architecture principles](ArchitecturePrinciples.md)
- [Capability dependency matrix](CapabilityDependencyMatrix.md)
- [Public capability contracts](PublicContracts.md)
- [Architecture fitness functions](FitnessFunctions.md)
- [Product readiness assessment](ProductReadinessAssessment.md)
- [Versioning and compatibility](Versioning.md)
- [Platform manifest](../PlatformManifest.md)
- [Platform Analytics v1](../PlatformAnalytics.md)
- [Capability map](../CapabilityMap.md)
- [Context map](../ContextMap.md)
- [Event catalog](../EventCatalog.md)

## Baseline topology

```mermaid
flowchart TD
    subgraph Client[Client Presentation]
        Studio[ConvoLab Studio (React 19 + Vite)]
    end

    subgraph APIHost[ASP.NET Core API Host]
        Middleware[Security Headers / Sanitizer / Rate Limiter / Auth]
        Endpoints[API Controllers & Endpoints]
        Middleware --> Endpoints
    end

    subgraph Core[Platform Core]
        Application[Application Contracts & Use Cases]
        Domain[Domain Aggregates & Invariants]
        Application --> Domain
    end

    subgraph Adapters[Infrastructure Adapters & Storage]
        Infrastructure[Infrastructure Repositories & Adapters]
        PostgreSQL[(PostgreSQL 16 DB)]
        LLM[Gemini / AI Providers]
        Storage[Document & Backup Stores]
        Infrastructure --> PostgreSQL
        Infrastructure --> LLM
        Infrastructure --> Storage
    end

    Studio -->|HTTPS / JSON API| Middleware
    Endpoints --> Application
    Infrastructure --> Application
    Infrastructure --> Domain
```

ConvoLab has one product frontend (`web/`) and one platform backend (`src/Api`). Provider SDKs and enterprise integrations must enter through Infrastructure or plugin adapters.
