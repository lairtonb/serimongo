namespace SeriMongo.Models
{
    public class ApplicationOptions
    {
        public DatabaseSettings Database { get; set; } = new DatabaseSettings();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Data Source=serimongo.db";
    }
}
