using SeriMongo.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Data
{
    public interface ITraceRepository
    {
        Task UpsertAsync(IReadOnlyCollection<TraceSpan> spans, CancellationToken cancellationToken = default);

        Task<TraceSearchPage> SearchAsync(TraceSearchFilter filter, CancellationToken cancellationToken = default);

        Task<TraceDetail> GetTraceAsync(string traceId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);
    }
}
