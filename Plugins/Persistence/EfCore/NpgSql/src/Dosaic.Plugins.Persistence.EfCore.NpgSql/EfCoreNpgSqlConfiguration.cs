using Dosaic.Hosting.Abstractions.Attributes;

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

}
