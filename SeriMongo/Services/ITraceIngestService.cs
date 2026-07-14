using SeriMongo.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public interface ITraceIngestService
    {
        Task<TraceIngestResult> IngestAsync(
            IReadOnlyCollection<TraceSpan> spans,
            CancellationToken cancellationToken = default);
    }
}
