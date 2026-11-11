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

    private int? _port;
    private int? _connectionLifetime;
    private int? _keepAlive;
    private int? _maxPoolSize;
    private bool? _includeErrorDetail;

    /// <summary>
    /// The postgres port. Defaults to 5432 unless connectionParameters set it.
    /// </summary>
    public int Port { get => _port ?? 5432; set => _port = value; }

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
    public int ConnectionLifetime { get => _connectionLifetime ?? 60; set => _connectionLifetime = value; }

    /// <summary>
    /// The number of seconds of connection inactivity before Npgsql sends a keepalive query. Set to 0 to disable. Defaults to 15 unless connectionParameters set it.
    /// </summary>
    public int KeepAlive { get => _keepAlive ?? 15; set => _keepAlive = value; }

    /// <summary>
    /// The maximum connection pool size. Defaults to 100 unless connectionParameters set it.
    /// </summary>
    public int MaxPoolSize { get => _maxPoolSize ?? 100; set => _maxPoolSize = value; }

    /// <summary>
    /// The time (in seconds) to cache the logging configuration. This is useful to prevent the logging configuration from being reloaded too often.
    /// </summary>
    public int ConfigureLoggingCacheTimeInSeconds { get; set; } = 300;

    /// <summary>
    /// When enabled, PostgreSQL error details are included on Detail and Detail. These can contain sensitive data.
    /// </summary>
    public bool IncludeErrorDetail { get => _includeErrorDetail ?? false; set => _includeErrorDetail = value; }

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

    /// <summary>
    /// Npgsql SslMode name (Disable, Allow, Prefer, Require, VerifyCA, VerifyFull). Empty (default) leaves Npgsql's own default in place.
    /// </summary>
    public string SslMode { get; set; }

    /// <summary>
    /// Path of the CA certificate file used to verify the server. Empty (default) = not set.
    /// </summary>
    public string RootCertificate { get; set; }

    /// <summary>
    /// Skips the DISCARD ALL Npgsql sends when a connection returns to the pool. Required behind pgBouncer in transaction mode.
    /// </summary>
    public bool NoResetOnClose { get; set; }

    /// <summary>
    /// Optional raw Npgsql connection string. It is applied first, every discrete setting that is configured overrides it. The defaults of port, connectionLifetime, keepAlive, maxPoolSize and includeErrorDetail only apply to what it leaves unset, so "Port=6432;Maximum Pool Size=20" works for pgBouncer.
    /// </summary>
    public string ConnectionParameters { get; set; }

    internal bool PortConfigured => _port.HasValue;
    internal bool ConnectionLifetimeConfigured => _connectionLifetime.HasValue;
    internal bool KeepAliveConfigured => _keepAlive.HasValue;
    internal bool MaxPoolSizeConfigured => _maxPoolSize.HasValue;
    internal bool IncludeErrorDetailConfigured => _includeErrorDetail.HasValue;
}
