# Platform Hardening Sprint 1 Report

## Delivered

- Canonical Prompt and Knowledge lifecycle policies owned by the Domain layer
- Application-layer use cases and repository ports for Simulator, Knowledge Studio, and Prompt Studio
- Entity Framework persistence adapters and migrations for the three functional surfaces
- Optimistic revision checks and structured concurrency conflicts
- RFC 7807 error responses with stable error codes and correlation IDs
- Liveness and readiness endpoints for database, document storage, and provider configuration
- Layered domain, application, architecture, API, and infrastructure test projects
- Frontend API client normalization and contract tests
- CI gates for repository hygiene, backend, frontend, and container builds

## Corrections applied while importing the release archive

- Replaced the invalid `UglyToad.PdfPig` package reference with stable `PdfPig` `0.1.15`
- Added the explicit HTTP client dependency required by the Infrastructure layer
- Added project-level xUnit imports to the new test projects
- Corrected simulation-store dependency injection and canonical active prompt status usage
- Renamed reserved MVC route tokens without changing public lifecycle endpoint URLs
- Preserved `application/problem+json` during JSON serialization
- Made API startup failures observable to hosts and integration tests
- Added a traditional .NET 8-compatible solution containing all nine projects

## Validation

Validated on Windows with .NET SDK 8.0.423 and Node.js 24:

- Backend restore and release build: passed
- Backend tests: 134 passed, 0 failed
- Frontend locked install: passed, 0 known vulnerabilities
- Frontend lint: passed
- Frontend production build: passed
- Frontend contract tests: passed

Docker Compose build and live endpoint checks were not run as part of this import and remain available through the scripts in `scripts/`.

## Follow-up

Workflow Designer is intentionally outside this archive. Its separately supplied implementation report describes a later slice, but no matching source bundle was included with Platform Hardening Sprint 1.
