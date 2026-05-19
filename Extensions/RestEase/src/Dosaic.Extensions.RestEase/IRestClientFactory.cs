namespace Dosaic.Extensions.RestEase
{
    public interface IRestClientFactory
    {
        T Create<T>(string name = null);
    }
}
