namespace Dosaic.Plugins.Persistence.VaultSharp.Types;

/// <summary>
/// The totp code details
/// </summary>
/// <param name="Code">the totp code</param>
/// <param name="ValidTillUtc">the utc date till the code is valid</param>
/// <param name="RemainingSeconds">the remaining seconds till the code is valid</param>
public record TotpCode(string Code, DateTime ValidTillUtc, int RemainingSeconds);

/// <summary>
/// The base 32 totp key to generate codes
/// </summary>
/// <param name="Base32Key">the base32 key</param>
public record TotpKey(string Base32Key);

/// <summary>
/// The totp details (either code for responses or the key for requests)
/// </summary>
/// <param name="TotpCode">The code details</param>
/// <param name="TotpKey">The key details</param>
public record Totp(TotpCode TotpCode, TotpKey TotpKey);

/// <summary>
/// Username + Password + Totp secret
/// </summary>
/// <param name="Username">the username</param>
/// <param name="Password">the password</param>
/// <param name="Totp">either the base totp key (for updates, creates) or the totp code for responses</param>
public record UsernamePasswordTotpSecret(string Username, string Password, Totp Totp) : UsernamePasswordSecret(Username, Password);
