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

        [TestCase("Meta Space", "A B", "Meta%20Space", "A%20B")]
        [TestCase("Key+Plus", "1+2", "Key%2BPlus", "1%2B2")]
        [TestCase("Slash/Back", "foo/bar", "Slash%2FBack", "foo%2Fbar")]
        [TestCase("Emoji😊", "x😊y", "Emoji%F0%9F%98%8A", "x%F0%9F%98%8Ay")]
        [TestCase("Umlauts ÄÖÜ", "äöü", "Umlauts%20%C3%84%C3%96%C3%9C", "%C3%A4%C3%B6%C3%BC")]
        public void EncodesKeyAndValueForCommonSpecials(string key, string value, string expectedKey, string expectedValue)
        {
            var blob = new BlobFile<SampleBucket>(SampleBucket.Logos);

            blob.AddMetaData(new KeyValuePair<string, string>(key, value));

            blob.MetaData.Should().ContainKey(expectedKey);
            blob.MetaData[expectedKey].Should().Be(expectedValue);
        }

        [Test]
        public void AddMetaDataEnumerableEncodesAllItems()
        {
            var blob = new BlobFile<SampleBucket>(SampleBucket.Logos);
            var items = new[]
            {
            new KeyValuePair<string, string>("Ä", "Ü"),
            new KeyValuePair<string, string>("Space Key", "A B"),
            new KeyValuePair<string, string>("Slash/Key", "val/ue"),
        };

            blob.AddMetaData(items);

            blob.MetaData.Should().ContainKeys("%C3%84", "Space%20Key", "Slash%2FKey");
            blob.MetaData["%C3%84"].Should().Be("%C3%9C");
            blob.MetaData["Space%20Key"].Should().Be("A%20B");
            blob.MetaData["Slash%2FKey"].Should().Be("val%2Fue");
        }

        [Test]
        public void AddMetaDataDuplicateKeyAfterEncodingThrows()
        {
            var blob = new BlobFile<SampleBucket>(SampleBucket.Logos);
            blob.AddMetaData(new KeyValuePair<string, string>("A B", "first"));

            Action act = () => blob.AddMetaData(new KeyValuePair<string, string>("A B", "second"));
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AddMetaDataEmptyEnumerableDoesNothing()
        {
            var blob = new BlobFile<SampleBucket>(SampleBucket.Logos);

            blob.AddMetaData(Array.Empty<KeyValuePair<string, string>>());

            blob.MetaData.Should().BeEmpty();
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
