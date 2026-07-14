using Microsoft.Data.Sqlite;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Data
{
    public sealed class SqliteTraceRepository : ITraceRepository
    {
        private const int ErrorStatusCode = 2;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly AppLogsContext _context;

        public SqliteTraceRepository(AppLogsContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(IReadOnlyCollection<TraceSpan> spans, CancellationToken cancellationToken = default)
        {
            if (spans == null)
            {
                throw new ArgumentNullException(nameof(spans));
            }

            if (spans.Count == 0)
            {
                return;
            }

            var traceIds = spans
                .Select(span => span.TraceId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (traceIds.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Every span must have a trace ID.", nameof(spans));
            }

            using var connection = _context.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            foreach (var span in spans)
            {
                await UpsertSpanAsync(connection, transaction, span, cancellationToken);
            }

            foreach (var traceId in traceIds)
            {
                await RefreshSummaryAsync(connection, transaction, traceId, cancellationToken);
            }

            transaction.Commit();
        }

        public async Task<TraceSearchPage> SearchAsync(TraceSearchFilter filter, CancellationToken cancellationToken = default)
        {
            filter ??= new TraceSearchFilter();

            var page = Math.Max(filter.Page, 1);
            var pageSize = Math.Clamp(filter.PageSize, 1, 500);
            var offset = (page - 1) * pageSize;
            var predicates = new List<string>();

            using var connection = _context.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();

            AddSearchPredicates(command, predicates, filter);
            command.CommandText = $@"
SELECT TraceId, StartTimestampUtc, EndTimestampUtc, DurationNanoseconds,
       RootSpanId, RootSpanName, RootServiceName, SpanCount, ErrorSpanCount,
       ServiceNamesJson, LastUpdatedUtc
FROM TraceSummaries
{BuildWhereClause(predicates)}
ORDER BY StartTimestampUtc DESC, TraceId ASC
LIMIT $limit OFFSET $offset;";
            command.Parameters.AddWithValue("$limit", pageSize);
            command.Parameters.AddWithValue("$offset", offset);

            var summaries = new List<TraceSummary>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                summaries.Add(ReadSummary(reader));
            }

            return new TraceSearchPage
            {
                Items = summaries,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<TraceDetail> GetTraceAsync(string traceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(traceId))
            {
                throw new ArgumentException("Trace ID is required.", nameof(traceId));
            }

            using var connection = _context.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var summary = await GetSummaryAsync(connection, traceId, cancellationToken);
            if (summary == null)
            {
                return null;
            }

            return new TraceDetail
            {
                Summary = summary,
                Spans = await GetSpansAsync(connection, transaction: null, traceId, cancellationToken)
            };
        }

        public async Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _context.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT DISTINCT ServiceName
FROM TraceSpans
WHERE TRIM(ServiceName) <> ''
ORDER BY ServiceName COLLATE NOCASE;";

            var serviceNames = new List<string>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                serviceNames.Add(reader.GetString(0));
            }

            return serviceNames;
        }

        private static async Task UpsertSpanAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            TraceSpan span,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO TraceSpans (
    TraceId, SpanId, ParentSpanId, TraceState, Flags, Name, Kind,
    StartTimestampUtc, EndTimestampUtc, DurationNanoseconds, StatusCode,
    StatusMessage, ServiceName, ResourceAttributesJson, ScopeName, ScopeVersion,
    AttributesJson, EventsJson, LinksJson, DroppedAttributesCount,
    DroppedEventsCount, DroppedLinksCount)
VALUES (
    $traceId, $spanId, $parentSpanId, $traceState, $flags, $name, $kind,
    $startTimestampUtc, $endTimestampUtc, $durationNanoseconds, $statusCode,
    $statusMessage, $serviceName, $resourceAttributesJson, $scopeName, $scopeVersion,
    $attributesJson, $eventsJson, $linksJson, $droppedAttributesCount,
    $droppedEventsCount, $droppedLinksCount)
ON CONFLICT(TraceId, SpanId) DO UPDATE SET
    ParentSpanId = excluded.ParentSpanId,
    TraceState = excluded.TraceState,
    Flags = excluded.Flags,
    Name = excluded.Name,
    Kind = excluded.Kind,
    StartTimestampUtc = excluded.StartTimestampUtc,
    EndTimestampUtc = excluded.EndTimestampUtc,
    DurationNanoseconds = excluded.DurationNanoseconds,
    StatusCode = excluded.StatusCode,
    StatusMessage = excluded.StatusMessage,
    ServiceName = excluded.ServiceName,
    ResourceAttributesJson = excluded.ResourceAttributesJson,
    ScopeName = excluded.ScopeName,
    ScopeVersion = excluded.ScopeVersion,
    AttributesJson = excluded.AttributesJson,
    EventsJson = excluded.EventsJson,
    LinksJson = excluded.LinksJson,
    DroppedAttributesCount = excluded.DroppedAttributesCount,
    DroppedEventsCount = excluded.DroppedEventsCount,
    DroppedLinksCount = excluded.DroppedLinksCount;";

            command.Parameters.AddWithValue("$traceId", span.TraceId);
            command.Parameters.AddWithValue("$spanId", span.SpanId);
            command.Parameters.AddWithValue("$parentSpanId", ToDatabaseValue(span.ParentSpanId));
            command.Parameters.AddWithValue("$traceState", span.TraceState ?? string.Empty);
            command.Parameters.AddWithValue("$flags", (long)span.Flags);
            command.Parameters.AddWithValue("$name", span.Name ?? string.Empty);
            command.Parameters.AddWithValue("$kind", span.Kind);
            command.Parameters.AddWithValue("$startTimestampUtc", FormatTimestamp(span.StartTimestamp));
            command.Parameters.AddWithValue("$endTimestampUtc", FormatTimestamp(span.EndTimestamp));
            command.Parameters.AddWithValue("$durationNanoseconds", span.DurationNanoseconds);
            command.Parameters.AddWithValue("$statusCode", span.StatusCode);
            command.Parameters.AddWithValue("$statusMessage", ToDatabaseValue(span.StatusMessage));
            command.Parameters.AddWithValue("$serviceName", span.ServiceName ?? string.Empty);
            command.Parameters.AddWithValue("$resourceAttributesJson", span.ResourceAttributesJson ?? "{}");
            command.Parameters.AddWithValue("$scopeName", ToDatabaseValue(span.ScopeName));
            command.Parameters.AddWithValue("$scopeVersion", ToDatabaseValue(span.ScopeVersion));
            command.Parameters.AddWithValue("$attributesJson", span.AttributesJson ?? "{}");
            command.Parameters.AddWithValue("$eventsJson", span.EventsJson ?? "[]");
            command.Parameters.AddWithValue("$linksJson", span.LinksJson ?? "[]");
            command.Parameters.AddWithValue("$droppedAttributesCount", (long)span.DroppedAttributesCount);
            command.Parameters.AddWithValue("$droppedEventsCount", (long)span.DroppedEventsCount);
            command.Parameters.AddWithValue("$droppedLinksCount", (long)span.DroppedLinksCount);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task RefreshSummaryAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string traceId,
            CancellationToken cancellationToken)
        {
            var spans = await GetSpansAsync(connection, transaction, traceId, cancellationToken);
            if (spans.Count == 0)
            {
                return;
            }

            var summary = CreateSummary(spans);
            await UpsertSummaryAsync(connection, transaction, summary, cancellationToken);
        }

        private static async Task<IReadOnlyList<TraceSpan>> GetSpansAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string traceId,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
SELECT TraceId, SpanId, ParentSpanId, TraceState, Flags, Name, Kind,
       StartTimestampUtc, EndTimestampUtc, DurationNanoseconds, StatusCode,
       StatusMessage, ServiceName, ResourceAttributesJson, ScopeName, ScopeVersion,
       AttributesJson, EventsJson, LinksJson, DroppedAttributesCount,
       DroppedEventsCount, DroppedLinksCount
FROM TraceSpans
WHERE TraceId = $traceId
ORDER BY StartTimestampUtc ASC, SpanId ASC;";
            command.Parameters.AddWithValue("$traceId", traceId);

            var spans = new List<TraceSpan>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                spans.Add(ReadSpan(reader));
            }

            return spans;
        }

        private static TraceSummary CreateSummary(IReadOnlyList<TraceSpan> spans)
        {
            var rootSpan = spans
                .Where(span => string.IsNullOrWhiteSpace(span.ParentSpanId))
                .OrderBy(span => span.StartTimestamp)
                .ThenBy(span => span.SpanId, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? spans
                    .OrderBy(span => span.StartTimestamp)
                    .ThenBy(span => span.SpanId, StringComparer.Ordinal)
                    .First();
            var startTimestamp = spans.Min(span => span.StartTimestamp);
            var endTimestamp = spans.Max(span => span.EndTimestamp);

            return new TraceSummary
            {
                TraceId = rootSpan.TraceId,
                StartTimestamp = startTimestamp,
                EndTimestamp = endTimestamp,
                DurationNanoseconds = CalculateDurationNanoseconds(startTimestamp, endTimestamp),
                RootSpanId = rootSpan.SpanId,
                RootSpanName = rootSpan.Name,
                RootServiceName = rootSpan.ServiceName,
                SpanCount = spans.Count,
                ErrorSpanCount = spans.Count(span => span.StatusCode == ErrorStatusCode),
                ServiceNames = spans
                    .Select(span => span.ServiceName)
                    .Where(serviceName => !string.IsNullOrWhiteSpace(serviceName))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(serviceName => serviceName, StringComparer.Ordinal)
                    .ToArray(),
                LastUpdated = DateTimeOffset.UtcNow
            };
        }

        private static async Task UpsertSummaryAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            TraceSummary summary,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO TraceSummaries (
    TraceId, StartTimestampUtc, EndTimestampUtc, DurationNanoseconds,
    RootSpanId, RootSpanName, RootServiceName, SpanCount, ErrorSpanCount,
    ServiceNamesJson, LastUpdatedUtc)
VALUES (
    $traceId, $startTimestampUtc, $endTimestampUtc, $durationNanoseconds,
    $rootSpanId, $rootSpanName, $rootServiceName, $spanCount, $errorSpanCount,
    $serviceNamesJson, $lastUpdatedUtc)
ON CONFLICT(TraceId) DO UPDATE SET
    StartTimestampUtc = excluded.StartTimestampUtc,
    EndTimestampUtc = excluded.EndTimestampUtc,
    DurationNanoseconds = excluded.DurationNanoseconds,
    RootSpanId = excluded.RootSpanId,
    RootSpanName = excluded.RootSpanName,
    RootServiceName = excluded.RootServiceName,
    SpanCount = excluded.SpanCount,
    ErrorSpanCount = excluded.ErrorSpanCount,
    ServiceNamesJson = excluded.ServiceNamesJson,
    LastUpdatedUtc = excluded.LastUpdatedUtc;";

            command.Parameters.AddWithValue("$traceId", summary.TraceId);
            command.Parameters.AddWithValue("$startTimestampUtc", FormatTimestamp(summary.StartTimestamp));
            command.Parameters.AddWithValue("$endTimestampUtc", FormatTimestamp(summary.EndTimestamp));
            command.Parameters.AddWithValue("$durationNanoseconds", summary.DurationNanoseconds);
            command.Parameters.AddWithValue("$rootSpanId", ToDatabaseValue(summary.RootSpanId));
            command.Parameters.AddWithValue("$rootSpanName", summary.RootSpanName);
            command.Parameters.AddWithValue("$rootServiceName", summary.RootServiceName);
            command.Parameters.AddWithValue("$spanCount", summary.SpanCount);
            command.Parameters.AddWithValue("$errorSpanCount", summary.ErrorSpanCount);
            command.Parameters.AddWithValue("$serviceNamesJson", JsonSerializer.Serialize(summary.ServiceNames, JsonOptions));
            command.Parameters.AddWithValue("$lastUpdatedUtc", FormatTimestamp(summary.LastUpdated));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<TraceSummary> GetSummaryAsync(
            SqliteConnection connection,
            string traceId,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT TraceId, StartTimestampUtc, EndTimestampUtc, DurationNanoseconds,
       RootSpanId, RootSpanName, RootServiceName, SpanCount, ErrorSpanCount,
       ServiceNamesJson, LastUpdatedUtc
FROM TraceSummaries
WHERE TraceId = $traceId;";
            command.Parameters.AddWithValue("$traceId", traceId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadSummary(reader) : null;
        }

        private static void AddSearchPredicates(
            SqliteCommand command,
            ICollection<string> predicates,
            TraceSearchFilter filter)
        {
            if (filter.StartTimestamp.HasValue)
            {
                predicates.Add("StartTimestampUtc >= $startTimestampUtc");
                command.Parameters.AddWithValue("$startTimestampUtc", FormatTimestamp(filter.StartTimestamp.Value));
            }

            if (filter.EndTimestamp.HasValue)
            {
                predicates.Add("StartTimestampUtc <= $endTimestampUtc");
                command.Parameters.AddWithValue("$endTimestampUtc", FormatTimestamp(filter.EndTimestamp.Value));
            }

            if (!string.IsNullOrWhiteSpace(filter.TraceId))
            {
                predicates.Add("TraceId = $traceId");
                command.Parameters.AddWithValue("$traceId", filter.TraceId);
            }

            if (!string.IsNullOrWhiteSpace(filter.ServiceName))
            {
                predicates.Add("RootServiceName = $serviceName");
                command.Parameters.AddWithValue("$serviceName", filter.ServiceName);
            }

            if (!string.IsNullOrWhiteSpace(filter.RootOperationName))
            {
                predicates.Add("RootSpanName LIKE $rootOperationName ESCAPE '\\'");
                command.Parameters.AddWithValue("$rootOperationName", $"%{EscapeLikePattern(filter.RootOperationName)}%");
            }

            if (filter.Status == TraceStatusFilter.Error)
            {
                predicates.Add("ErrorSpanCount > 0");
            }
            else if (filter.Status == TraceStatusFilter.Successful)
            {
                predicates.Add("ErrorSpanCount = 0");
            }

            if (filter.MinimumDurationNanoseconds.HasValue)
            {
                predicates.Add("DurationNanoseconds >= $minimumDurationNanoseconds");
                command.Parameters.AddWithValue("$minimumDurationNanoseconds", filter.MinimumDurationNanoseconds.Value);
            }

            if (filter.MaximumDurationNanoseconds.HasValue)
            {
                predicates.Add("DurationNanoseconds <= $maximumDurationNanoseconds");
                command.Parameters.AddWithValue("$maximumDurationNanoseconds", filter.MaximumDurationNanoseconds.Value);
            }
        }

        private static string BuildWhereClause(IReadOnlyCollection<string> predicates)
        {
            return predicates.Count == 0
                ? string.Empty
                : $"WHERE {string.Join(" AND ", predicates)}";
        }

        private static TraceSummary ReadSummary(SqliteDataReader reader)
        {
            return new TraceSummary
            {
                TraceId = reader.GetString(0),
                StartTimestamp = ParseTimestamp(reader.GetString(1)),
                EndTimestamp = ParseTimestamp(reader.GetString(2)),
                DurationNanoseconds = reader.GetInt64(3),
                RootSpanId = reader.IsDBNull(4) ? null : reader.GetString(4),
                RootSpanName = reader.GetString(5),
                RootServiceName = reader.GetString(6),
                SpanCount = reader.GetInt32(7),
                ErrorSpanCount = reader.GetInt32(8),
                ServiceNames = JsonSerializer.Deserialize<List<string>>(reader.GetString(9), JsonOptions) ?? new List<string>(),
                LastUpdated = ParseTimestamp(reader.GetString(10))
            };
        }

        private static TraceSpan ReadSpan(SqliteDataReader reader)
        {
            return new TraceSpan
            {
                TraceId = reader.GetString(0),
                SpanId = reader.GetString(1),
                ParentSpanId = reader.IsDBNull(2) ? null : reader.GetString(2),
                TraceState = reader.GetString(3),
                Flags = checked((uint)reader.GetInt64(4)),
                Name = reader.GetString(5),
                Kind = reader.GetInt32(6),
                StartTimestamp = ParseTimestamp(reader.GetString(7)),
                EndTimestamp = ParseTimestamp(reader.GetString(8)),
                DurationNanoseconds = reader.GetInt64(9),
                StatusCode = reader.GetInt32(10),
                StatusMessage = reader.IsDBNull(11) ? null : reader.GetString(11),
                ServiceName = reader.GetString(12),
                ResourceAttributesJson = reader.GetString(13),
                ScopeName = reader.IsDBNull(14) ? null : reader.GetString(14),
                ScopeVersion = reader.IsDBNull(15) ? null : reader.GetString(15),
                AttributesJson = reader.GetString(16),
                EventsJson = reader.GetString(17),
                LinksJson = reader.GetString(18),
                DroppedAttributesCount = checked((uint)reader.GetInt64(19)),
                DroppedEventsCount = checked((uint)reader.GetInt64(20)),
                DroppedLinksCount = checked((uint)reader.GetInt64(21))
            };
        }

        private static string FormatTimestamp(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static DateTimeOffset ParseTimestamp(string value)
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static long CalculateDurationNanoseconds(DateTimeOffset startTimestamp, DateTimeOffset endTimestamp)
        {
            var duration = endTimestamp - startTimestamp;
            if (duration.Ticks <= 0)
            {
                return 0;
            }

            return duration.Ticks > long.MaxValue / 100
                ? long.MaxValue
                : duration.Ticks * 100;
        }

        private static object ToDatabaseValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        }

        private static string EscapeLikePattern(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }
    }
}