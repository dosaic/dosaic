using AwesomeAssertions;
using Dosaic.Plugins.Persistence.S3.Blob;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests.Blob
{
    public class BlobFileMetaDataStoreTests
    {
        [Test]
        public void TryGetValueReturnsTrueAndDecodedWhenPresent()
        {
            var store = new BlobFileMetaDataStore();
            store.Set("Ä Key", "Ü Value");

            var ok = store.TryGetValue("Ä Key", out var value);

            ok.Should().BeTrue();
            value.Should().Be("Ü Value");
        }

        [Test]
        public void TryGetValueReturnsFalseWhenMissing()
        {
            var store = new BlobFileMetaDataStore();

            var ok = store.TryGetValue("does-not-exist", out var value);

            ok.Should().BeFalse();
            value.Should().BeNull();
        }

        [Test]
        public void IndexerGetSetRoundtripsDecoded()
        {
            var store = new BlobFileMetaDataStore();

            store["Foo Ä"] = "Bär Ü";

            store["Foo Ä"].Should().Be("Bär Ü");

            var encoded = store.GetUrlEncodedMetadata();
            encoded.Keys.Should().Contain("Foo%20%C3%84");
            encoded["Foo%20%C3%84"].Should().Be("B%C3%A4r%20%C3%9C");
        }

        [Test]
        public void IndexerSetNullRemovesEntry()
        {
            var store = new BlobFileMetaDataStore();
            store["to-remove"] = "value";

            store["to-remove"] = null;

            store.TryGetValue("to-remove", out var removed).Should().BeFalse();
            removed.Should().BeNull();
            store.GetUrlEncodedMetadata().Should().NotContainKey("to-remove"); // also not present in any encoded form
        }

        [Test]
        public void GetEncodedMetadataReturnsCopyNotBacked()
        {
            var store = new BlobFileMetaDataStore();
            store.Set(new KeyValuePair<string, string>("K", "V"));

            var snapshot1 = store.GetUrlEncodedMetadata();
            snapshot1["K"].Should().Be("V");

            snapshot1["K"] = "CHANGED";

            var snapshot2 = store.GetUrlEncodedMetadata();
            snapshot2["K"].Should().Be("V");

            store.Get("K").Should().Be("V");
        }

        [Test]
        public void ContainsKeyDoesFunctionAsExpected()
        {
            var store = new BlobFileMetaDataStore();
            store.Set("key", "value");

            store.ContainsKey("key").Should().BeTrue();
            store.ContainsKey("noKey").Should().BeFalse();
        }

        [Test]
        public void GetMetadataReturnsDecodedCopyNotBacked()
        {
            var store = new BlobFileMetaDataStore();
            store.Set("Ä", "Ü");

            var decoded = store.GetMetadata();
            decoded.Should().ContainKey("Ä");
            decoded["Ä"].Should().Be("Ü");

            decoded["Ä"] = "CHANGED";

            store.Get("Ä").Should().Be("Ü");
        }

        [Test]
        public void SetEnumerableEncodesAllItems()
        {
            var store = new BlobFileMetaDataStore();

            store.Set(new[]
            {
                new KeyValuePair<string, string>("Ä", "Ü"),
                new KeyValuePair<string, string>("A B", "C D"),
                new KeyValuePair<string, string>("Slash/Key", "val/ue"),
            });

            var encoded = store.GetUrlEncodedMetadata();
            encoded.Should().ContainKeys("%C3%84", "A%20B", "Slash%2FKey");
            encoded["%C3%84"].Should().Be("%C3%9C");
            encoded["A%20B"].Should().Be("C%20D");
            encoded["Slash%2FKey"].Should().Be("val%2Fue");

            var decoded = store.GetMetadata();
            decoded.Should().ContainKeys("Ä", "A B", "Slash/Key");
            decoded["Ä"].Should().Be("Ü");
            decoded["A B"].Should().Be("C D");
            decoded["Slash/Key"].Should().Be("val/ue");
        }
    }
}
