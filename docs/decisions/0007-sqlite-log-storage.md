# ADR 0007: SQLite FTS5 log storage

**Status:** Accepted for milestone 3 — 2026-08-03

Persist parsed log events and raw messages in SQLite with FTS5, WAL mode, and batched writes. Keep ingestion bounded so UI and processes cannot be blocked by storage. Default retention is 14 days or 2 GB, whichever is reached first, with per-profile configuration and export before cleanup.
