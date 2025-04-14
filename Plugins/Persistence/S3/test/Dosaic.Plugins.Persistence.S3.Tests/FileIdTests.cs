using Dosaic.Extensions.Sqids;
using Dosaic.Plugins.Persistence.S3.File;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests
{
    public class FileIdTests
    {
        [Test]
        public void CanBeTranslatedToSqid()
        {
            var fileId = new FileId<SampleBucket>(SampleBucket.Logos, "123");
            var stringId = fileId.Id;
            var newSecretId = FileId<SampleBucket>.FromSqid(stringId);
            newSecretId.Should().Be(fileId); // eGcaFP58ojhMILeDDblrzRd
        }

        [Test]
        public void TryParseReturnsTrueForValidFileId()
        {
            FileId<SampleBucket>.TryParse("eGcaFP58ojhMILeDDblrzRd", out var fileId).Should().BeTrue();
            var stringId = fileId.Id;
            var newSecretId = FileId<SampleBucket>.FromSqid(stringId);
            newSecretId.Should().Be(fileId);
        }

        [Test]
        public void TryParseReturnsFalseForInvalidFileId()
        {
            FileId<SampleBucket>.TryParse("something:key", out var fileId).Should().BeFalse();
            var stringId = fileId.Id;
            var newSecretId = FileId<SampleBucket>.FromSqid(stringId);
            newSecretId.Should().NotBe(fileId);
        }

        [Test]
        public void CanGenerateNewIds()
        {
            var id = FileId<SampleBucket>.New(SampleBucket.Logos);
            id.Bucket.Should().Be(SampleBucket.Logos);
            id.Key.Should().NotBeEmpty();
            Guid.TryParse(id.Key, out _).Should().BeTrue();
            id.Id.FromSqid().Should().NotBeEmpty().And.Contain(id.Key).And
                .StartWith(id.Bucket.ToString());
        }
    }
}
