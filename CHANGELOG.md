# Changelog

All notable changes to SeriMongo are documented in this file.

The format follows Keep a Changelog, and the project intends to use Semantic
Versioning when numbered releases begin.

## [Unreleased]

### Added

- SQLite-backed log storage with optional idempotent seed data.
- Parameterized LogQL search with field, property, comparison, text, and
  boolean operators.
- OTLP/HTTP JSON ingestion through `/v1/logs` and `/otlp/v1/logs`.
- SignalR-based realtime tailing with server-side query synchronization.
- Paged and virtualized log results with configurable columns.
- Quick filters for periods, levels, services, and structured properties.
- Structured event details, Markdown copy actions, and an in-app help guide.
- A standalone simulator for individual events, mixed bursts, and continuous
  traffic.
- Docker images and a Docker Compose environment for the viewer and simulator.

### Changed

- Upgraded the backend to .NET 10 and the frontend to Angular 22.
- Redesigned the viewer as a dense log-inspection workspace with search,
  filters, results, and event details on one screen.
- Updated the README for the current architecture, development workflow, and
  user interface.

### Removed

- The former MongoDB storage implementation and related dependencies.
- Legacy .NET Core 3.1 samples, the Angular 9 sandbox, and the 2020 Insomnia
  collection.
- Outdated MongoDB screenshots and completed internal planning documents.
