using Dosaic.Hosting.Abstractions.Middlewares.Models;
using Dosaic.Plugins.Persistence.S3.File;

namespace Dosaic.Plugins.Persistence.S3.Tests;

public enum SampleBucket
{
    [FileBucket("logos", FileType.Images)] Logos = 0,

    [FileBucket("docs", FileType.Documents)]
    Documents
}
