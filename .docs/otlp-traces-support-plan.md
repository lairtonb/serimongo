# OTLP Traces Support Plan

- Status: proposed
- Created: 2026-07-13
- Scope: OTLP/HTTP trace ingestion, SQLite persistence, query APIs, log
  correlation, and trace visualization in the Angular UI.

## Goal

Replace the current trace accept-and-drop compatibility endpoints with a real
OTLP trace receiver. A user must be able to export traces with a standard .NET
OpenTelemetry `http/protobuf` exporter, find a trace in SeriMongo, inspect its
span hierarchy and timing, and investigate the trace beside its correlated
logs in the existing workspace.

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
10. Keep logs and traces in the same workspace at `/`. Do not add separate
    `/logs` or `/traces` UI routes; use `?traceId=<id>` for shareable trace
    focus.
11. Render the trace panel above the log grid so the waterfall and correlated
    logs remain visible together.
12. Opening a trace temporarily replaces the active LogQL scope with an exact
    `prop.traceId` query. Closing the trace restores the previous query,
    filters, tail state, and scroll position.

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

## Phase 4: Unified Logs and Traces Workspace

Keep the current log workspace at `/` and add a trace panel between the query
panel and the log grid. The panel is hidden by default and has browse, loading,
selected trace, not found, and error states.

Layout and interaction:

- Open the panel above the log grid with a default height of 300 pixels, a
  minimum of 180 pixels, and a maximum of 60 percent of the workspace height.
- Add a horizontal splitter for resizing. Below 980 pixels, hide the splitter,
  use a bounded automatic height, and allow horizontal waterfall scrolling.
- Add a `Traces` action beside `Search` and `Tail`. It opens an integrated
  recent-trace browser without changing LogQL until a trace is selected.
- Provide compact trace filters for exact trace ID, service, status, minimum
  duration, and period. Selecting a result enters the same trace-focus flow as
  selecting a TraceId from a log.
- Show start time, duration, root service and operation, span/service counts,
  error state, and an abbreviated copyable trace ID in browse results.
- Render the selected trace as a collapsible span tree and horizontal
  waterfall aligned to the trace start and duration.
- Keep service colors stable within a trace. Show span service, operation,
  kind, status, duration, and proportional timing.
- Mark orphan spans and clock skew explicitly instead of producing broken
  geometry.
- Reuse the resizable right sidebar for either log details or selected-span
  details. Selecting one clears the other selection but does not close the
  trace panel.
- Show span attributes, resource attributes, scope, events, links, status,
  IDs, and copy actions in the selected-span view.
- Implement the waterfall with HTML and CSS first. Do not add a charting
  dependency unless performance testing demonstrates a need.

Component boundaries:

- Keep `HomeComponent` as the workspace orchestrator.
- Extract trace browsing, the trace panel, the waterfall, and span details into
  focused standalone components.
- Add a dedicated Angular `TraceService`; do not add trace responsibilities to
  the existing log `SearchService`.

Exit criteria:

- A trace with hundreds of spans remains scrollable and selection stays
  responsive.
- Waterfall bars have accessible text equivalents and keyboard selection.
- Traces without correlated logs are discoverable through the integrated
  browser.
- Logs and the selected trace remain visible together without navigating to a
  separate page.

## Phase 5: Trace Focus, LogQL Correlation, and Realtime Updates

Trace links and focus lifecycle:

- Render a canonical 32-character lowercase hexadecimal TraceId as a real
  hyperlink to `/?traceId=<id>`. Invalid or missing IDs remain plain text.
- Stop TraceId link activation from also selecting the containing log row.
- Before entering trace focus, snapshot the manual query, period, level,
  service and property filters, tail state, selected log, and log-grid scroll
  offset.
- Pause tailing, suspend quick filters, set the visible query to
  `prop.traceId = "<trace-id>"`, and fetch trace details and the first log page
  in parallel.
- Allow the user to edit LogQL while the trace remains pinned. Those temporary
  edits are discarded when trace focus closes.
- Switching to another trace updates the trace and LogQL without replacing the
  original pre-focus snapshot.
- Closing the panel or returning to trace browse restores the snapshot,
  reruns the previous query, restores the scroll offset, and resumes tailing
  only if it was active before trace focus.
- Restore the previously selected log only when it exists in the reloaded
  results; otherwise leave the details sidebar empty.
- Opening `/?traceId=<id>` directly starts with a trace-only query and no
  snapshot. Closing a direct link returns to `*`.
- Keep URL state synchronized with panel state so browser back and forward open
  and close trace focus correctly.
- If logs arrive before trace storage, keep the LogQL results visible and show
  `Trace data not found yet` with a retry action.

Correlation and realtime behavior:

- Add `Filter span logs` to the selected-span view. It changes the temporary
  query to `prop.traceId = "<trace-id>" and prop.spanId = "<span-id>"`.
- Publish a `TraceListItem` SignalR notification after a trace ingest
  transaction commits.
- Keep trace notifications separate from existing LogQL tail subscriptions.
- Debounce and refetch an open trace after notifications rather than applying
  individual span mutations in the browser.
- Refresh the integrated recent-trace browser without reloading the page.

Exit criteria:

- A log opens its trace and exact correlated LogQL scope in one action.
- Closing trace focus restores the complete previous log investigation state.
- A span filters its correlated logs in one action.
- Direct links and browser history reproduce the selected trace state.
- New and updated traces appear without a full page reload.

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

- integrated trace browsing, filtering, and paging
- TraceId validation, hyperlink semantics, and row-click isolation
- temporary LogQL generation, snapshot restoration, and safe escaping
- tree construction, orphan handling, and waterfall geometry
- selected-span details, copy actions, and span-log filtering
- loading, not-found, retry, and error panel states
- direct-link, new-tab, and browser back/forward behavior
- responsive panel sizing and horizontal waterfall scrolling
- accessibility checks for links, splitters, tree rows, and waterfall
  alternatives

### End-to-end container verification

1. Start the rebuilt image with `serimongo-data` attached.
2. Export a multi-service trace from a .NET sample using `http/protobuf`.
3. Confirm the exporter receives a successful OTLP response.
4. Confirm the trace and all spans exist through the API and integrated panel.
5. Click a log TraceId, confirm the exact temporary LogQL query and waterfall
   above the correlated logs, then close it and verify state restoration.
6. Open a trace without logs through integrated browse and through a direct
   `?traceId=` link.
7. Select a span and confirm the span-specific LogQL filter.
8. Restart the container and confirm the trace remains available.

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
4. `feat(ui): integrate trace panel and waterfall`
5. `feat(ui): add trace log focus and realtime updates`
6. `docs(traces): document receiver, retention, and verification`

Each commit must leave tests passing. Do not switch `/v1/traces` from
accept-and-drop to persistence until the migration, repository, and endpoint
tests are part of the same deployable sequence.

## Definition of Done

- OTLP/HTTP Protobuf and JSON traces are persisted through both route aliases.
- Success, partial success, malformed input, and unsupported media responses
  follow OTLP/HTTP semantics.
- Existing SQLite log data survives migration and container recreation.
- Trace browse, detail tree, waterfall, filters, and copy actions work above the
  existing log grid without a separate trace page.
- Canonical TraceIds are hyperlinks that open shareable `?traceId=` focus and
  apply an exact temporary LogQL scope.
- Closing trace focus restores query, quick filters, tail state, selection, and
  scroll position according to the focus snapshot rules.
- Traces without logs remain accessible through integrated browse.
- Log-to-trace and span-to-log correlation works with canonical IDs.
- Automated mapper, repository, endpoint, UI, and container verification cover
  the complete flow.
- README and CHANGELOG no longer describe traces as discarded.

## Decisions Required Before Implementation

- Default trace retention age and optional maximum trace count.
- Maximum accepted request bytes and spans per export request.
- Whether partial traces should be visually marked after an inactivity window,
  and the length of that window.
