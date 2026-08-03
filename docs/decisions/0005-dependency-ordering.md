# ADR 0005: Dependency graph ordering

**Status:** Accepted — 2026-08-03

Services use explicit dependency edges and stable priority for independent nodes. Validation rejects cycles and unknown dependencies. Future bulk startup will follow topological layers and configured concurrency; shutdown will reverse successful start order.
