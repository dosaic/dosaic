using AwesomeAssertions;
using Dosaic.Plugins.Persistence.S3.Blob;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests.Blob
{
    public class BlobFileTests
    {
        [TestCase("test.jpg", ".jpg")]
        [TestCase("testÄ.png", ".png")]
        [TestCase("report.final.tar.gz", ".gz")]
        public void CreateWithFileNameSetsMetadataFileName(string fileName, string extension)
        {
            var withFilename = new BlobFile<SampleBucket>(SampleBucket.Logos).WithFilename(fileName);

            withFilename.MetaData[BlobFileMetaData.Filename].Should().Be(fileName);
            withFilename.MetaData[BlobFileMetaData.FileExtension].Should().Be(extension);
        }

        [TestCase(null)]
        [TestCase("")]
        public void CreateWthFileNameEmptyOrNullSetsNoMetadate(string value)
        {
            var withFilename = new BlobFile<SampleBucket>(SampleBucket.Logos).WithFilename(value);
            var tryGetValue = withFilename.MetaData.TryGetValue(BlobFileMetaData.Filename, out var outValue);
            tryGetValue.Should().BeFalse();
            outValue.Should().BeNull();
        }

        [TestCase("test.jpg", ".jpg")]
        [TestCase(".Äo", ".Äo")]
        [TestCase(".jpg", ".jpg")]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void CreateWithFileExtensionSetsMetadataFileExtension(string inputFilename, string expectedExtension)
        {

            var withFilename = new BlobFile<SampleBucket>(SampleBucket.Logos).WithFileExtension(inputFilename);

            var tryGetFileName = withFilename.MetaData.TryGetValue(BlobFileMetaData.FileExtension, out var withFilenameResult);
            if (expectedExtension != null)
            {
                tryGetFileName.Should().BeTrue();
                withFilenameResult.Should().Be(expectedExtension);
            }
            else
            {
                tryGetFileName.Should().BeFalse();
                withFilenameResult.Should().BeNull();
            }
        }

        [Test]
        public void CreateWithoutFileNameCreatesEmptyMetadata()
        {
            var blobFile = new BlobFile<SampleBucket>(SampleBucket.Logos);
            blobFile.MetaData.GetMetadata().Should().BeEmpty();
        }

        [Test]
        public void AddMetaDataEmptyEnumerableDoesNothing()
        {
            var blob = new BlobFile<SampleBucket>(SampleBucket.Logos);

            blob.MetaData.Set(Array.Empty<KeyValuePair<string, string>>());

            blob.MetaData.GetMetadata().Should().BeEmpty();
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
