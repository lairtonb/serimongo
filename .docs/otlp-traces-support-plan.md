# OTLP Traces Support Plan

- Status: proposed
- Created: 2026-07-13
- Scope: OTLP/HTTP trace ingestion, SQLite persistence, query APIs, log
  correlation, and trace visualization in the Angular UI.

## Goal

Replace the current trace accept-and-drop compatibility endpoints with a real
OTLP trace receiver. A user must be able to export traces with a standard .NET
OpenTelemetry `http/protobuf` exporter, find a trace in SeriMongo, inspect its
span hierarchy and timing, and navigate between a trace and its correlated
logs.

Metrics remain outside this plan. Their compatibility endpoints continue to
acknowledge and discard payloads until a separate metrics design is approved.

## Current Baseline

- `/v1/traces` and `/otlp/v1/traces` accept OTLP/HTTP Protobuf or JSON and
  return an empty successful response, but discard the request body.
- OTLP log records already preserve lowercase hexadecimal `traceId` and
  `spanId` properties. This provides the correlation key for the first UI
  integration.
- SQLite initialization is currently an idempotent inline `CREATE TABLE IF NOT
  EXISTS` script in `AppLogsContext`; there is no schema version table.
- The Angular application has one log workspace at `/`, backed by LogQL search
  and a SignalR tail.
- The development volume `serimongo-data` must survive every schema and image
  upgrade.

## Protocol and Product Decisions

1. Support both OTLP/HTTP transports already advertised by SeriMongo:
   `application/x-protobuf` and OTLP JSON.
2. Keep both route prefixes: `/v1/traces` and `/otlp/v1/traces`.
3. Store trace and span IDs as validated lowercase hexadecimal text. A trace ID
   must decode to 16 bytes and a span ID to 8 bytes.
4. Treat spans as the source of truth and maintain one derived summary row per
   trace for efficient list queries.
5. Make ingestion idempotent on `(TraceId, SpanId)`. Retries must update the
   span rather than create duplicates.
6. Accept out-of-order batches. Recompute the affected trace summary in the
   same transaction after upserting a batch.
7. Return an OTLP `ExportTraceServiceResponse` in the same representation as
   the request. Use `partial_success` when individual spans are rejected; use
   HTTP 400 only when the request itself cannot be decoded.
8. Keep trace search deliberately smaller than LogQL for the first release.
   Add a trace-specific query language only after the filter model stabilizes.
9. Do not add trace data to `LogEntries.PropertiesJson`. Trace persistence gets
   dedicated tables and repositories.

## Target Backend Shape

The intended request path is:

1. `OtlpTracesController` selects Protobuf or JSON from the content type.
2. A protocol-specific mapper produces canonical `TraceSpan` models.
3. `ITraceIngestService` validates and groups spans by trace ID.
4. `ITraceRepository` upserts spans and refreshes trace summaries in one
   SQLite transaction.
5. `ITraceNotifier` publishes changed summaries after the transaction commits.
6. Query controllers return list and detail DTOs designed for the UI rather
   than exposing persistence models directly.

Keep the trace controller separate from `OtlpLogsController`. When trace
ingestion is enabled, remove the trace attributes from `AcceptAndDrop`; leave
the metric attributes there until metrics are implemented.

## SQLite Schema and Migration Foundation

Introduce a minimal forward-only schema migrator before adding trace tables.
It should create a `SchemaMigrations` table, run numbered SQL migrations in a
transaction, and record each successful migration. Existing databases without
the table are version zero and retain all `LogEntries` rows.

### `TraceSummaries`

One row per trace, optimized for the trace list:

- `TraceId TEXT PRIMARY KEY`
- `StartTimestampUtc TEXT NOT NULL`
- `EndTimestampUtc TEXT NOT NULL`
- `DurationNanoseconds INTEGER NOT NULL`
- `RootSpanId TEXT NULL`
- `RootSpanName TEXT NOT NULL`
- `RootServiceName TEXT NOT NULL`
- `SpanCount INTEGER NOT NULL`
- `ErrorSpanCount INTEGER NOT NULL`
- `ServiceNamesJson TEXT NOT NULL`
- `LastUpdatedUtc TEXT NOT NULL`

Indexes:

- `StartTimestampUtc DESC`
- `RootServiceName, StartTimestampUtc DESC`
- `ErrorSpanCount, StartTimestampUtc DESC`
- `DurationNanoseconds, StartTimestampUtc DESC`

### `TraceSpans`

One row per span, preserving the OTLP data needed by the detail UI:

- `TraceId TEXT NOT NULL`
- `SpanId TEXT NOT NULL`
- `ParentSpanId TEXT NULL`
- `TraceState TEXT NOT NULL`
- `Flags INTEGER NOT NULL`
- `Name TEXT NOT NULL`
- `Kind INTEGER NOT NULL`
- `StartTimestampUtc TEXT NOT NULL`
- `EndTimestampUtc TEXT NOT NULL`
- `DurationNanoseconds INTEGER NOT NULL`
- `StatusCode INTEGER NOT NULL`
- `StatusMessage TEXT NULL`
- `ServiceName TEXT NOT NULL`
- `ResourceAttributesJson TEXT NOT NULL`
- `ScopeName TEXT NULL`
- `ScopeVersion TEXT NULL`
- `AttributesJson TEXT NOT NULL`
- `EventsJson TEXT NOT NULL`
- `LinksJson TEXT NOT NULL`
- OTLP dropped-attribute, dropped-event, and dropped-link counts
- `PRIMARY KEY (TraceId, SpanId)`

Indexes:

- `TraceId, StartTimestampUtc`
- `ParentSpanId`
- `ServiceName, StartTimestampUtc DESC`
- `StatusCode, StartTimestampUtc DESC`

Span events and links can remain JSON in the first release. They are inspected
in a selected-span panel and are not initial search dimensions. Normalize them
later only if real query requirements justify the additional tables.

## Phase 1: Contracts and Persistence

- Vendor the matching official OpenTelemetry trace proto files alongside the
  existing v1.9.0 common, resource, and log definitions:
  `collector/trace/v1/trace_service.proto` and `trace/v1/trace.proto`.
- Add canonical domain models and API DTOs independently from generated
  Protobuf types.
- Implement the forward-only migration runner and trace tables.
- Implement `ITraceRepository` with batch upsert, summary recomputation, list
  search, trace detail, and service-name queries.
- Select a root span deterministically: a span without `ParentSpanId`, then the
  earliest span as a fallback for incomplete traces.
- Add repository integration tests using temporary SQLite databases, including
  upgrade from a logs-only database and preservation of existing logs.

Exit criteria:

- An existing `serimongo-data` database upgrades without data loss.
- Replaying the same batch leaves one row per span.
- Late child or root spans update the summary duration and counts correctly.

## Phase 2: OTLP Trace Ingestion

- Add `OtlpProtobufTraceMapper` for native generated types. Preserve resource
  attributes, instrumentation scope, span kind, status, events, links, trace
  state, flags, and dropped counts.
- Add an OTLP JSON mapper without changing the existing log JSON path.
- Add `ITraceIngestService` to validate IDs and timestamps, group spans, and
  coordinate transactional writes.
- Add `OtlpTracesController` on both route aliases.
- Enforce configurable request and batch limits to avoid unbounded buffering.
- Return matching empty Protobuf or JSON responses on complete success.
- Populate `partial_success.rejected_spans` and a bounded error message when a
  request contains both valid and invalid spans.
- Continue returning 415 for unsupported content types and 400 for malformed
  Protobuf or JSON bodies.
- Emit operational counters or structured logs for received, persisted,
  rejected, and failed spans. Do not log span payload bodies.

Exit criteria:

- The standard .NET OTLP `http/protobuf` exporter can send spans without
  retries or protocol warnings.
- JSON and Protobuf representations produce equivalent canonical spans.
- Both trace route aliases pass the same endpoint contract tests.

## Phase 3: Query API

Add trace-focused endpoints:

- `GET /api/traces` returns paged `TraceListItem` rows.
- `GET /api/traces/{traceId}` returns a trace summary and all spans ordered by
  start time.
- `GET /api/traces/services` returns distinct service names for quick filters.

Initial list filters:

- UTC start/end window
- exact trace ID
- service name
- root operation name substring
- status: all, error, or successful
- minimum and maximum duration
- page and page size, capped server-side

Return enough span data for the browser to build a tree, but also include a
server-computed `Depth` and orphan marker so malformed or incomplete parent
relationships cannot break the UI. Use DTOs with explicit JSON contracts and
never interpolate filter values into SQL.

Exit criteria:

- Pagination is stable under identical timestamps.
- Query plans use the trace-summary indexes for common period, service, error,
  and duration filters.
- Unknown, malformed, and syntactically valid but absent trace IDs have
  distinct 400 and 404 behavior.

## Phase 4: Angular Trace Workspace

Split the current single page into an application shell with primary `Logs`
and `Traces` navigation. Preserve the current log workspace behavior and add:

- `/logs` for the existing log viewer, with `/` redirecting to `/logs`.
- `/traces` for trace search and recent traces.
- `/traces/:traceId` for a shareable selected trace.

The trace list should show:

- start timestamp
- total duration
- root service and operation
- number of spans and services
- error state
- abbreviated trace ID with copy action

The trace detail should show:

- a collapsible span tree
- a horizontal waterfall aligned to the trace start and duration
- service-specific colors that remain consistent within the trace
- span name, service, kind, duration, and status
- a selected-span panel with attributes, resource attributes, scope, events,
  links, status message, IDs, and copy actions
- explicit orphan and clock-skew indicators rather than broken geometry

Reuse the current dense workspace layout, resizable detail sidebar, virtualized
rows, quick-filter styling, and accessible keyboard controls. Implement the
waterfall with HTML/CSS positioning first; do not add a charting dependency
unless performance testing shows it is necessary.

Exit criteria:

- A trace with hundreds of spans remains scrollable and selection stays
  responsive.
- Waterfall bars have accessible text equivalents and keyboard selection.
- Direct navigation to `/traces/:traceId` restores the selected trace.

## Phase 5: Log Correlation and Realtime Updates

- Make every non-empty trace ID in the log table and detail panel a link to
  `/traces/:traceId`.
- Add `View related logs` to the trace and selected-span panels. Navigate to
  `/logs` with a LogQL query for `prop.traceId` and, for a span, `prop.spanId`.
- Teach the log workspace to initialize its query from route query parameters
  without weakening LogQL escaping.
- Add a trace SignalR notification carrying a `TraceListItem` after an ingest
  transaction commits.
- Keep trace subscriptions separate from existing LogQL tail subscriptions.
  The first version may refresh an already visible trace rather than stream
  individual span mutations.

Exit criteria:

- A log can open its trace in one action.
- A span can open its correlated logs in one action.
- New traces appear in an active recent-traces view without a full page reload.

## Test Matrix

### Mapper tests

- Protobuf and JSON parity
- resource and scope propagation
- all span kinds and status codes
- trace/span IDs and parent relationships
- attributes, arrays, key-value lists, and bytes
- events, event attributes, links, and link attributes
- nanosecond timestamps and duration
- invalid ID lengths, invalid time ranges, and missing resources/scopes

### Repository tests

- empty-database creation and logs-only migration
- idempotent replay
- out-of-order batches and late root spans
- concurrent batches for one trace
- summary counts, duration, errors, and service names
- pagination and every supported filter
- cancellation and transaction rollback

### Endpoint tests

- Protobuf and JSON on both route aliases
- matching response content type and parseable response body
- malformed body 400 and unsupported content type 415
- partial success with rejected span count
- configured request-size and batch-size limits

### UI tests

- trace list filtering and paging
- tree construction, orphan handling, and waterfall geometry
- selected-span details and copy actions
- log-to-trace and trace-to-log navigation
- deep-link restoration
- accessibility checks for navigation, tree rows, and waterfall alternatives

### End-to-end container verification

1. Start the rebuilt image with `serimongo-data` attached.
2. Export a multi-service trace from a .NET sample using `http/protobuf`.
3. Confirm the exporter receives a successful OTLP response.
4. Confirm the trace and all spans exist through the API and UI.
5. Follow a trace-linked log into the trace view and back to related logs.
6. Restart the container and confirm the trace remains available.

## Rollout and Operational Safeguards

- Back up a representative development volume before testing the first schema
  migration.
- Add trace retention settings before enabling production-like traffic:
  maximum age, optional maximum trace count, and cleanup batch size.
- Run retention cleanup in bounded transactions and delete spans before their
  summaries.
- Document approximate storage growth from a representative EnduroPlatform
  workload before choosing defaults.
- Expose receiver state in documentation and diagnostics so an operator can
  distinguish `accepted and stored` from the former `accepted and dropped`
  behavior.
- Rebuild and recreate the development container after each backend/UI delivery
  that must be verified in Docker.

## Recommended Delivery Commits

1. `feat(traces): add schema migrations and trace storage`
2. `feat(traces): ingest OTLP HTTP protobuf and JSON spans`
3. `feat(traces): expose trace query APIs`
4. `feat(ui): add trace list and waterfall details`
5. `feat(ui): correlate logs and traces`
6. `docs(traces): document receiver, retention, and verification`

Each commit must leave tests passing. Do not switch `/v1/traces` from
accept-and-drop to persistence until the migration, repository, and endpoint
tests are part of the same deployable sequence.

## Definition of Done

- OTLP/HTTP Protobuf and JSON traces are persisted through both route aliases.
- Success, partial success, malformed input, and unsupported media responses
  follow OTLP/HTTP semantics.
- Existing SQLite log data survives migration and container recreation.
- Trace list, detail tree, waterfall, filters, deep links, and copy actions work
  in the UI.
- Log-to-trace and trace-to-log correlation works with canonical IDs.
- Automated mapper, repository, endpoint, UI, and container verification cover
  the complete flow.
- README and CHANGELOG no longer describe traces as discarded.

## Decisions Required Before Implementation

- Default trace retention age and optional maximum trace count.
- Maximum accepted request bytes and spans per export request.
- Whether partial traces should be visually marked after an inactivity window,
  and the length of that window.
- Whether realtime trace updates are required in the first UI release or may
  follow the searchable list/detail MVP.
