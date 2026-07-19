# ADR 0016: Optimistic concurrency

**Status:** Accepted

Mutable Prompt and Knowledge resources carry a monotonic revision. Mutations include an expected revision and stale writes fail with a structured 409 conflict. This avoids hidden last-write-wins data loss.
