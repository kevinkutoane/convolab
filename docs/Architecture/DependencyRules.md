# Dependency Rules

```text
Studio -> API contracts
API -> Application + Infrastructure composition
Infrastructure -> Application + Domain
Application -> Domain
Domain -> nothing
```

Application repository contracts expose domain-oriented state and operations. They must not expose `DbContext`, `DbSet`, `IQueryable`, change trackers, SQL, or provider exceptions. Infrastructure records are not domain aggregates. Provider SDKs and document libraries remain isolated in Infrastructure.
