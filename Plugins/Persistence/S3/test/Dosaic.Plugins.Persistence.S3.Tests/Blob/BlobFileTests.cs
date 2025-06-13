using Dosaic.Plugins.Persistence.S3.Blob;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests.Blob
{
    public class BlobFileTests
    {
        [Test]
        public void CreateWithFileNameSetsMetadataFileName()
        {
            var fileName = "test.jpg";
            var withFilename = new BlobFile<SampleBucket>(SampleBucket.Logos).WithFilename(fileName);

            withFilename.MetaData[BlobFileMetaData.Filename].Should().Be(fileName);
            withFilename.MetaData[BlobFileMetaData.FileExtension].Should().Be(".jpg");

            var blobFile = new BlobFile("mybucket", "mykey").WithFilename(fileName);
            blobFile.MetaData[BlobFileMetaData.Filename].Should().Be(fileName);
            blobFile.MetaData[BlobFileMetaData.FileExtension].Should().Be(".jpg");
        }

        [TestCase("test.jpg", ".jpg")]
        [TestCase(".jpg", ".jpg")]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void CreateWithFileExtensionSetsMetadataFileExtension(string inputFilename, string expectedExtension)
        {

            var withFilename = new BlobFile<SampleBucket>(SampleBucket.Logos).WithFileExtension(inputFilename);
            var tryGetFileName = withFilename.MetaData.TryGetValue(BlobFileMetaData.FileExtension, out var withFilenameResult);
            if (expectedExtension != null && tryGetFileName)
            {
                withFilenameResult.Should().Be(expectedExtension);
            }

            var withFileExtension = new BlobFile("mybucket", "mykey").WithFileExtension(inputFilename);
            var tryGetFileExtension = withFileExtension.MetaData.TryGetValue(BlobFileMetaData.FileExtension, out var withFileExtensionResult);
            if (expectedExtension != null && tryGetFileExtension)
            {
                withFileExtensionResult.Should().Be(expectedExtension);
            }
        }

        [Test]
        public void CreateWithoutFileNameCreatesEmptyMetadata()
        {
            var blobFile = new BlobFile<SampleBucket>(SampleBucket.Logos);
            blobFile.MetaData.Should().BeEmpty();
        }

        [Test]
        public void CreateSetsIdCorrectly()
        {
            var id = Guid.NewGuid().ToString();
            var blobFile = new BlobFile<SampleBucket>(SampleBucket.Documents, id);
            blobFile.Id.Key.Should().Be(id);
        }

        [Test]
        public void CreateSetsLastModifiedToCurrentUtcTime()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);
            var blobFile = new BlobFile<SampleBucket>(SampleBucket.Logos);
            var after = DateTimeOffset.UtcNow.AddSeconds(1);
            blobFile.LastModified.Should().BeOnOrAfter(before);
            blobFile.LastModified.Should().BeOnOrBefore(after);
        }
    }
}
