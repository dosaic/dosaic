namespace Dosaic.Plugins.Persistence.VaultSharp.Secret;

public enum SecretType
{
    UsernamePassword = 0,
    UsernamePasswordTotp = 1,
    UsernamePasswordApiKey = 2,
    Certificate = 3,
}
