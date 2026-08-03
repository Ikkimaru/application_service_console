# ADR 0003: Dynamic service discovery

**Status:** Accepted — 2026-08-03

Profiles declare discovery providers rather than service inventories. Maven/Spring discovery traverses declared reactor modules and recognizes application entry points. Maven coordinates provide stable identity. Editor metadata may enrich discovered services but cannot create them, preventing phantom services from stale configuration.

Newly discovered services require explicit enablement and startup metadata before bulk execution.
