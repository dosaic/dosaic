using AwesomeAssertions;
using Dosaic.Plugins.Persistence.S3.Blob;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests.Blob
{
    public class BlobFileTests
    {
        [TestCase("test.jpg", "test.jpg", ".jpg")]
        [TestCase("testÄ.jpg", "test%C3%84.jpg", ".jpg")]
        public void CreateWithFileNameSetsMetadataFileName(string fileName, string expectedFileName, string extension)
        {
            var withFilename = new BlobFile<SampleBucket>(SampleBucket.Logos).WithFilename(fileName);

            withFilename.MetaData[BlobFileMetaData.Filename].Should().Be(expectedFileName);
            withFilename.MetaData[BlobFileMetaData.FileExtension].Should().Be(extension);

            var blobFile = new BlobFile("mybucket", "mykey").WithFilename(fileName);
            blobFile.MetaData[BlobFileMetaData.Filename].Should().Be(expectedFileName);
            blobFile.MetaData[BlobFileMetaData.FileExtension].Should().Be(extension);
        }

        [TestCase("test.jpg", ".jpg")]
        [TestCase(".Äo", ".%C3%84o")]
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
