namespace Dosaic.Plugins.Persistence.VaultSharp.Types;

/// <summary>
/// Certificate + Passphrase (optional) secret
/// </summary>
/// <param name="Certificate">the certificate</param>
/// <param name="Passphrase">the passphrase (optional)</param>
public record CertificateSecret(string Certificate, string Passphrase = null) : Secret.Secret;
