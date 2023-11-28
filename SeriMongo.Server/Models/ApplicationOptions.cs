using MongoDB.Driver;

namespace SeriMongo.Models
{
    public class ApplicationOptions
    {        
        public DatabaseSettings ConnectionInfo { get; set; }
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
        public string CollectionName { get; set; }
    }
}
