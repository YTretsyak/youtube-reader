using MongoDB.Driver;

namespace Infrastructure.Persistence;

public static class MongoClientSettingsFactory
{
    public static MongoClientSettings Create(string connectionString)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);

        // Single `api` instance, low request volume (personal/portfolio project, not a
        // high-traffic production service) — scaled down from the mongodb-connection skill's
        // default traditional-long-running-server recommendation (maxPoolSize 50+, minPoolSize
        // 10-20). Revisit if the app is ever deployed with multiple api replicas or real traffic.
        settings.MaxConnectionPoolSize = 20;
        settings.MinConnectionPoolSize = 0;
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);

        return settings;
    }
}
