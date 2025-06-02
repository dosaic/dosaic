using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests
{
    public class BlobFileTests
    {
        [Test]
        public void CreateWithFileNameSetsMetadataWithFileName()
        {
            var id = new FileId<SampleBucket>(SampleBucket.Logos, Guid.NewGuid().ToString());
            var fileName = "test.jpg";

            var blobFileSambleBucket = BlobFile<SampleBucket>.Create(id, fileName);

            blobFileSambleBucket.MetaData.Should().ContainKey(BlobFileMetaData.Filename);
            blobFileSambleBucket.MetaData[BlobFileMetaData.Filename].Should().Be(fileName);

            var blobFile = BlobFile.Create(new FileId("mybucket", "mykey", FileType.Images), fileName);

            blobFile.MetaData.Should().ContainKey(BlobFileMetaData.Filename);
            blobFile.MetaData[BlobFileMetaData.Filename].Should().Be(fileName);
        }

        [Test]
        public void CreateWithoutFileNameCreatesEmptyMetadata()
        {
            var id = new FileId<SampleBucket>(SampleBucket.Logos, Guid.NewGuid().ToString());

            var blobFile = BlobFile<SampleBucket>.Create(id);

            blobFile.MetaData.Should().BeEmpty();
        }

        [Test]
        public void CreateSetsIdCorrectly()
        {
            var id = new FileId<SampleBucket>(SampleBucket.Documents, Guid.NewGuid().ToString());

            var blobFile = BlobFile<SampleBucket>.Create(id);

            blobFile.Id.Should().Be(id);
        }

        [Test]
        public void CreateSetsLastModifiedToCurrentUtcTime()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);
            var id = new FileId<SampleBucket>(SampleBucket.Logos, Guid.NewGuid().ToString());

            var blobFile = BlobFile<SampleBucket>.Create(id);

            var after = DateTimeOffset.UtcNow.AddSeconds(1);
            blobFile.LastModified.Should().BeOnOrAfter(before);
            blobFile.LastModified.Should().BeOnOrBefore(after);
        }

        [Test]
        public void CreateWithNullFileNameDoesNotThrow()
        {
            var id = new FileId<SampleBucket>(SampleBucket.Documents, Guid.NewGuid().ToString());

            Action action = () => BlobFile<SampleBucket>.Create(id, null);

            action.Should().NotThrow();
        }
    }
}
