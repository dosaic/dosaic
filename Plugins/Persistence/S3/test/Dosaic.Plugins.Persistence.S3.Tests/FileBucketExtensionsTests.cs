using Dosaic.Plugins.Persistence.S3.File;
using FluentAssertions;
using MimeDetective.Definitions;
using MimeDetective.Storage;
using NUnit.Framework;
using FileType = Dosaic.Plugins.Persistence.S3.File.FileType;

namespace Dosaic.Plugins.Persistence.S3.Tests
{
    public class FileBucketExtensionsTests
    {
        [Test]
        public void GetNameReturnsCorrectNameFromAttribute()
        {
            SampleBucket.Logos.GetName().Should().Be("test-logos");
        }

        [Test]
        public void GetFileTypeReturnsCorrectFileTypeFromAttribute()
        {
            SampleBucket.Logos.GetFileType().Should().Be(FileType.Images);
        }

        [Test]
        public void GetDefinitionsForAllFileTypeReturnsAllDefinitions()
        {
            var defs = FileType.All.GetDefinitions();

            defs.Should().NotBeEmpty();
            defs.Should().BeEquivalentTo(DefaultDefinitions.All());
        }

        [Test]
        public void GetDefinitionsForSpecificFileTypeReturnsMatchingDefinitions()
        {
            var defs = FileType.Images.GetDefinitions();

            defs.Should().NotBeEmpty();
            defs.Should().BeEquivalentTo(DefaultDefinitions.FileTypes.Images.All());
        }

        [Test]
        public void GetDefinitionsForNoneFileTypeReturnsEmptyCollection()
        {
            var defs = FileType.None.GetDefinitions();

            defs.Should().BeEmpty();
        }

        [Test]
        public void GetDefinitionsForMultipleFileTypesReturnsCombinedDefinitions()
        {
            var combinedType = FileType.Xml | FileType.Documents;
            var defs = combinedType.GetDefinitions();

            var expectedDefs = new List<Definition>();
            expectedDefs.AddRange(DefaultDefinitions.FileTypes.Xml.All());
            expectedDefs.AddRange(DefaultDefinitions.FileTypes.Documents.All());

            defs.Should().NotBeEmpty();
            defs.Should().BeEquivalentTo(expectedDefs);
        }

        [Test]
        public void GetDefinitionsForBucketReturnsDefinitionsMatchingBucketFileType()
        {
            var fileTypeDefs = SampleBucket.Logos.GetFileType().GetDefinitions();

            fileTypeDefs.Should().BeEquivalentTo(DefaultDefinitions.FileTypes.Images.All());
        }
    }
}
