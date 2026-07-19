# Architecture Fitness Functions

Architecture fitness functions are automated or reviewable checks that prevent architectural drift.

## Automated rules

1. Domain projects reference no API, Infrastructure, UI, persistence, or vendor SDK assemblies.
2. Application references Domain only, except approved cross-cutting abstractions.
3. API references Application and composes Infrastructure through dependency injection.
4. Provider and connector implementations remain in Infrastructure or plugin assemblies.
5. Bounded contexts do not reference each other's internal aggregate types.
6. No circular project or namespace dependencies.
7. Domain events are immutable business facts.
8. Aggregate state mutation occurs through behaviour methods, not public setters.
9. Studio source contains no provider-selection, workflow-progression, retrieval-ranking, or policy business logic.
10. Every public Studio response is an API DTO rather than a serialized domain aggregate.

## Build gates

### Platform

- Restore and compile all .NET projects.
- Run Domain tests.
- Run Architecture tests.
- Fail the build on warnings selected by repository policy.

### Studio

- `npm ci`
- `npm run lint`
- `npm run build`
- Component and accessibility tests as product surfaces become interactive.

## Documentation gates

A new capability is incomplete until it documents:

- purpose and non-goals;
- public capabilities;
- commands and queries;
- events published and consumed;
- dependencies and forbidden dependencies;
- extension points;
- lifecycle and sequence diagrams;
- relevant ADRs.

## Review questions

- Does the change extend Platform Core or consume it?
- Is the new concept part of the ubiquitous language?
- Is a new dependency necessary and allowed?
- Can a provider, channel, or persistence implementation be replaced?
- Is the operation reproducible and observable?
- Is governance encoded as behaviour rather than prose only?
