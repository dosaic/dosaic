using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Jobs.TickerQ
{
    [Configuration("tickerq")]
    public class TickerQConfiguration
    {
        public string Host { get; set; }
        public int Port { get; set; } = 5432;
        public string Database { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public bool InMemory { get; set; }
        public bool UseRedis { get; set; }
        public string RedisConnectionString { get; set; }
        public string Schema { get; set; } = "ticker";
        public string AllowedDashboardHost { get; set; }
        public string DashboardBasePath { get; set; } = "/tickerq/dashboard";
        public DashboardAuthMode DashboardAuthMode { get; set; } = DashboardAuthMode.Host;
        public string DashboardUsername { get; set; }
        public string DashboardPassword { get; set; }
        public string DashboardApiKey { get; set; }
        public bool EnableJobsByFeatureManagementConfig { get; set; }
        public int? MaxConcurrency { get; set; }
        public int? PollingIntervalInMs { get; set; }
        public string SchedulerTimeZone { get; set; }

        public string ConnectionString =>
            $"Host={Host};Port={Port};Database={Database};Username={User};Password={Password};";
    }

    public enum DashboardAuthMode
    {
        None,
        Basic,
        ApiKey,
        Host
    }
}
