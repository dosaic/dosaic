namespace Dosaic.Plugins.Persistence.Smb
{
    public interface ISmbStorage
    {
        Task WriteAsync(string filePath, byte[] content, CancellationToken cancellationToken = default);
        Task<Stream> ReadStreamAsync(string filePath, CancellationToken cancellationToken = default);
        Task EnsurePathAsync(string path, CancellationToken cancellationToken = default);
        Task DeleteIfExists(string filePath, CancellationToken cancellationToken = default);
    }
}
