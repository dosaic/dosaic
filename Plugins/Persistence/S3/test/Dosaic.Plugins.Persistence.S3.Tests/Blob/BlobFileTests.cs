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

            withFilename.EncodedMetaData[BlobFileMetaData.Filename].Should().Be(expectedFileName);
            withFilename.EncodedMetaData[BlobFileMetaData.FileExtension].Should().Be(extension);

            withFilename.MetaData[BlobFileMetaData.Filename].Should().Be(fileName);
            withFilename.MetaData[BlobFileMetaData.FileExtension].Should().Be(extension);

            var blobFile = new BlobFile("mybucket", "mykey").WithFilename(fileName);
            blobFile.EncodedMetaData[BlobFileMetaData.Filename].Should().Be(expectedFileName);
            blobFile.EncodedMetaData[BlobFileMetaData.FileExtension].Should().Be(extension);

            blobFile.MetaData[BlobFileMetaData.Filename].Should().Be(fileName);
            blobFile.MetaData[BlobFileMetaData.FileExtension].Should().Be(extension);
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

        [TestCase("test.jpg", ".jpg", ".jpg")]
        [TestCase(".Äo", ".%C3%84o", ".Äo")]
        [TestCase(".jpg", ".jpg", ".jpg")]
        [TestCase("", null, null)]
        [TestCase(null, null, null)]
        public void CreateWithFileExtensionSetsMetadataFileExtension(string inputFilename, string expectedExtensionEncoded, string expectedExtension)
        {

            var withFilename = new BlobFile<SampleBucket>(SampleBucket.Logos).WithFileExtension(inputFilename);
            var tryGetEncodedFileName = withFilename.EncodedMetaData.TryGetValue(BlobFileMetaData.FileExtension, out var withEncodedFilenameResult);
            var tryGetFileName = withFilename.MetaData.TryGetValue(BlobFileMetaData.FileExtension, out var withFilenameResult);
            if (expectedExtension != null)
            {
                tryGetFileName.Should().BeTrue();
                tryGetEncodedFileName.Should().BeTrue();
                withFilenameResult.Should().Be(expectedExtension);
                withEncodedFilenameResult.Should().Be(expectedExtensionEncoded);
            }
            else
            {
                tryGetFileName.Should().BeFalse();
                tryGetEncodedFileName.Should().BeFalse();
                withFilenameResult.Should().BeNull();
                withEncodedFilenameResult.Should().BeNull();
            }

            var withFileExtension = new BlobFile("mybucket", "mykey").WithFileExtension(inputFilename);
            var tryGetEncodedFileExtension = withFileExtension.EncodedMetaData.TryGetValue(BlobFileMetaData.FileExtension, out var withEncodedFileExtensionResult);
            var tryGetFileExtension = withFileExtension.MetaData.TryGetValue(BlobFileMetaData.FileExtension, out var withFileExtensionResult);

            if (expectedExtension != null)
            {
                tryGetFileExtension.Should().BeTrue();
                tryGetEncodedFileExtension.Should().BeTrue();
                withFileExtensionResult.Should().Be(expectedExtension);
                withEncodedFileExtensionResult.Should().Be(expectedExtensionEncoded);
            }
            else
            {
                tryGetFileExtension.Should().BeFalse();
                tryGetEncodedFileExtension.Should().BeFalse();
                withFileExtensionResult.Should().BeNull();
                withEncodedFileExtensionResult.Should().BeNull();
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

            blob.EncodedMetaData.Should().ContainKey(expectedKey);
            blob.EncodedMetaData[expectedKey].Should().Be(expectedValue);
            blob.MetaData.Should().ContainKey(key);
            blob.MetaData[key].Should().Be(value);
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

            blob.EncodedMetaData.Should().ContainKeys("%C3%84", "Space%20Key", "Slash%2FKey");
            blob.EncodedMetaData["%C3%84"].Should().Be("%C3%9C");
            blob.EncodedMetaData["Space%20Key"].Should().Be("A%20B");
            blob.EncodedMetaData["Slash%2FKey"].Should().Be("val%2Fue");
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
