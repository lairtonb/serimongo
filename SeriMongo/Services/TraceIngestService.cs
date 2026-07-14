using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public sealed class TraceIngestService : ITraceIngestService
    {
        private readonly ITraceRepository _traceRepository;

        public TraceIngestService(ITraceRepository traceRepository)
        {
            _traceRepository = traceRepository;
        }

        public async Task<TraceIngestResult> IngestAsync(
            IReadOnlyCollection<TraceSpan> spans,
            CancellationToken cancellationToken = default)
        {
            if (spans == null)
            {
                throw new ArgumentNullException(nameof(spans));
            }

            var validSpans = new List<TraceSpan>(spans.Count);
            var result = new TraceIngestResult();

            foreach (var span in spans)
            {
                if (TryValidate(span, out var errorMessage))
                {
                    validSpans.Add(span);
                    continue;
                }

                result.RejectedSpans++;
                result.ErrorMessage ??= errorMessage;
            }

            if (validSpans.Count > 0)
            {
                await _traceRepository.UpsertAsync(validSpans, cancellationToken);
                result.PersistedSpans = validSpans.Count;
            }

            return result;
        }

        private static bool TryValidate(TraceSpan span, out string errorMessage)
        {
            if (!IsLowercaseHex(span.TraceId, 32))
            {
                errorMessage = "Trace IDs must be 32-character lowercase hexadecimal values.";
                return false;
            }

            if (!IsLowercaseHex(span.SpanId, 16))
            {
                errorMessage = "Span IDs must be 16-character lowercase hexadecimal values.";
                return false;
            }

            if (!string.IsNullOrEmpty(span.ParentSpanId) && !IsLowercaseHex(span.ParentSpanId, 16))
            {
                errorMessage = "Parent span IDs must be 16-character lowercase hexadecimal values.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(span.Name))
            {
                errorMessage = "Span names are required.";
                return false;
            }

            if (span.EndTimestamp < span.StartTimestamp)
            {
                errorMessage = "Span end timestamps cannot precede start timestamps.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool IsLowercaseHex(string value, int length)
        {
            if (string.IsNullOrEmpty(value) || value.Length != length)
            {
                return false;
            }

            foreach (var character in value)
            {
                if ((character < '0' || character > '9') && (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
