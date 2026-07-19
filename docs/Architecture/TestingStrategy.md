# Testing Strategy

ConvoLab uses layered tests:

1. **Domain tests** verify invariants, lifecycle policies, immutability, semantic versions, deterministic rendering, retrieval, and package sealing.
2. **Application tests** verify use-case orchestration against in-memory test doubles.
3. **Infrastructure integration tests** verify persistence, migrations, storage, extraction, retrieval adapters, and provider routing.
4. **API integration tests** verify HTTP contracts, Problem Details, health endpoints, and validation.
5. **Architecture tests** protect project dependency rules.
6. **Studio tests** verify typed contracts, lifecycle controls, errors, and offline behavior.

Release gates are defined in `.github/workflows/ci.yml`. PostgreSQL/Testcontainers coverage remains a required follow-up before production certification.
