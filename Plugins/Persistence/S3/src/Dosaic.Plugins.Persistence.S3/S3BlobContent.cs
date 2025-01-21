using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Persistence.S3
{
    internal class S3BlobContent : IBlobContent
    {
        public Stream Data { get; set; }
        public Dictionary<string, string> MetaData { get; set; }
    }
}
