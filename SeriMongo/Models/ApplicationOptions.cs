namespace SeriMongo.Models
{
    public class ApplicationOptions
    {
        public DatabaseSettings Database { get; set; } = new DatabaseSettings();

        public SeedSettings Seed { get; set; } = new SeedSettings();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Data Source=serimongo.db";
    }

    public class SeedSettings
    {
        public bool Enabled { get; set; }

        public string ScriptPath { get; set; } = "seed.sql";
    }
}
