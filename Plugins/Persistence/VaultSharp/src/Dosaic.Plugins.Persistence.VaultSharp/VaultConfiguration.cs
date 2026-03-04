using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Persistence.VaultSharp;

[Configuration("vault")]
public class VaultConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the Vault server.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// The token used to authenticate with the Vault server.
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the issuing organization.
    /// Defaults to "Dosaic.Plugins.Persistence.VaultSharp".
    /// </summary>
    public string TotpIssuer { get; set; } = "Dosaic.Plugins.Persistence.VaultSharp";

    /// <summary>
    /// Specifies the length of time in seconds used to generate a counter for the TOTP code calculation.
    /// Defaults to 30 seconds
    /// </summary>
    public int TotpPeriodInSeconds { get; set; } = 30;

    /// <summary>
    /// When true, uses a local filesystem folder for secret storage instead of a Vault server.
    /// </summary>
    public bool UseLocalFileSystem { get; set; }

    /// <summary>
    /// Root folder for storing secrets when <see cref="UseLocalFileSystem"/> is true.
    /// Defaults to "./nodep-vault".
    /// </summary>
    public string LocalFileSystemPath { get; set; } = "./nodep-vault";
}
