namespace SeriMongo.Services
{
    public sealed class TraceIngestResult
    {
        public int PersistedSpans { get; set; }

        public int RejectedSpans { get; set; }

        public string ErrorMessage { get; set; }
    }
}
