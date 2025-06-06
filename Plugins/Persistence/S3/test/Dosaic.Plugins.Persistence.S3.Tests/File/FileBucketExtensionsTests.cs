using Dosaic.Plugins.Persistence.S3.File;
using FluentAssertions;
using NUnit.Framework;
using FileType = Dosaic.Plugins.Persistence.S3.File.FileType;

namespace Dosaic.Plugins.Persistence.S3.Tests.File
{
    public class FileBucketExtensionsTests
    {
        [Test]
        public void GetNameReturnsCorrectNameFromAttribute()
        {
            SampleBucket.Logos.GetName().Should().Be("logos");
        }

        [Test]
        public void GetFileTypeReturnsCorrectFileTypeFromAttribute()
        {
            SampleBucket.Logos.GetFileType().Should().Be(FileType.Images);
        }

    }
}
