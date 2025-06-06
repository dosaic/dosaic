using Dosaic.Hosting.Abstractions.Attributes;
using Hangfire.States;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    [Configuration("hangfire")]
    public class HangfireConfiguration
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Database { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public bool InMemory { get; set; }
        public string AllowedDashboardHost { get; set; }
        public bool EnableJobsByFeatureManagementConfig { get; set; }
        public int? PollingIntervalInMs { get; set; }
        public int? WorkerCount { get; set; }
        public string[] Queues { get; set; } = [EnqueuedState.DefaultQueue];
        public int InvisibilityTimeoutInMinutes { get; set; } = 30;
        public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={User};Password={Password};";
        public int MaxJobArgumentsSizeToRenderInBytes { get; set; } = 4096;
    }
}
