namespace Dosaic.Plugins.Persistence.VaultSharp.Types;

/// <summary>
/// Username + Password + ApiKey secret
/// </summary>
/// <param name="Username">the username</param>
/// <param name="Password">the password</param>
/// <param name="ApiKey">the apiKey</param>
public record UsernamePasswordApiKeySecret(string Username, string Password, string ApiKey) : UsernamePasswordSecret(Username, Password);
