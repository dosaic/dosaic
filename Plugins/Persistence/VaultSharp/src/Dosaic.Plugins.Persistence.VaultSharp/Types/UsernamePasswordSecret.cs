namespace Dosaic.Plugins.Persistence.VaultSharp.Types;

/// <summary>
/// Username + Password secret
/// </summary>
/// <param name="Username">the username</param>
/// <param name="Password">the password</param>
public record UsernamePasswordSecret(string Username, string Password) : Secret.Secret;
