using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql;

/// <summary>
/// Configuration for the persistence layer
/// </summary>
[Configuration("npgsql")]
// ReSharper disable once ClassNeverInstantiated.Global
public class EfCoreNpgSqlConfiguration
{
    /// <summary>
    /// The postgres host
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// The postgres port
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// The postgres database
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// The postgres username
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// The postgres password
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// The total maximum lifetime of connections (in seconds). Connections which have exceeded this value will be destroyed instead of returned from the pool. This is useful in clustered configurations to force load balancing between a running server and a server just brought online. It can also be useful to prevent runaway memory growth of connections at the PostgreSQL server side, because in some cases very long lived connections slowly consume more and more memory over time. Defaults to 3600 seconds (1 hour).
    /// <value>The time (in seconds) to wait, or 0 to to make connections last indefinitely.</value>
    /// </summary>
    public int ConnectionLifetime { get; set; } = 60;

    /// <summary>
    /// The number of seconds of connection inactivity before Npgsql sends a keepalive query. Set to 0 (the default) to disable.
    /// </summary>
    public int KeepAlive { get; set; } = 15;

    /// <summary>
    /// The maximum connection pool size.
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// The time (in seconds) to cache the logging configuration. This is useful to prevent the logging configuration from being reloaded too often.
    /// </summary>
    public int ConfigureLoggingCacheTimeInSeconds { get; set; } = 300;

    /// <summary>
    /// When enabled, PostgreSQL error details are included on Detail and Detail. These can contain sensitive data.
    /// </summary>
    public bool IncludeErrorDetail { get; set; }

    /// <summary>
    /// When enabled, detailed errors are included in the exception messages. These can contain sensitive data.
    /// </summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>
    /// When enabled, sensitive data logging is included in the exception messages. These can contain sensitive data.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// When enabled, the query splitting behavior is set to split query. This is useful for large queries that can be split into multiple queries.
    /// </summary>
    public bool SplitQuery { get; set; }
}
