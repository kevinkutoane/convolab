# ADR 0015: Application dependency rule

**Status:** Accepted

Application references Domain and application frameworks only. Entity Framework, document parsers, provider SDKs, HTTP clients, and concrete storage implementations are prohibited and protected by architecture tests.
