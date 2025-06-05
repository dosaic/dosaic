using Dosaic.Plugins.Persistence.S3.File;

namespace Dosaic.Plugins.Persistence.S3.Tests;

public enum SampleBucket
{
    [FileBucket("test-logos", FileType.Images)]
    Logos = 0,

    [FileBucket("test.docs", FileType.Documents)]
    Documents
}
