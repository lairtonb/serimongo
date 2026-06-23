using SeriMongo.Querying;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SeriMongo.Services
{
    public interface ITailSubscriptionStore
    {
        void Set(string connectionId, string queryText, LogSqlQuery query);

        void Remove(string connectionId);

        IReadOnlyList<TailSubscription> GetSnapshot();
    }

    public class TailSubscriptionStore : ITailSubscriptionStore
    {
        private readonly ConcurrentDictionary<string, TailSubscription> _subscriptions = new ConcurrentDictionary<string, TailSubscription>();

        public void Set(string connectionId, string queryText, LogSqlQuery query)
        {
            _subscriptions[connectionId] = new TailSubscription(connectionId, queryText, query);
        }

        public void Remove(string connectionId)
        {
            _subscriptions.TryRemove(connectionId, out _);
        }

        public IReadOnlyList<TailSubscription> GetSnapshot()
        {
            return _subscriptions.Values.ToArray();
        }
    }

    public class TailSubscription
    {
        public TailSubscription(string connectionId, string queryText, LogSqlQuery query)
        {
            ConnectionId = connectionId;
            QueryText = queryText;
            Query = query;
        }

        public string ConnectionId { get; }

        public string QueryText { get; }

        public LogSqlQuery Query { get; }
    }
}
