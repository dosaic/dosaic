using Dosaic.Plugins.Persistence.S3.File;
using FluentAssertions;
using MimeDetective.Definitions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests.File
{
    public class DefaultFileTypeDefinitionResolverTests
    {
        [Test]
        public void ShouldResolveDefaultFileTypeDefinitionsCorrectly()
        {
            var resolver = new DefaultFileTypeDefinitionResolver();

            resolver.GetDefinitions(FileType.Any).Should().BeEmpty();
            resolver.GetDefinitions(FileType.Archives).Should()
                .BeEquivalentTo(DefaultDefinitions.FileTypes.Archives.All());
            resolver.GetDefinitions(FileType.Documents).Should()
                .BeEquivalentTo(DefaultDefinitions.FileTypes.Documents.All());
            resolver.GetDefinitions(FileType.Email).Should()
                .BeEquivalentTo(DefaultDefinitions.FileTypes.Email.All());
            resolver.GetDefinitions(FileType.Images).Should()
                .BeEquivalentTo(DefaultDefinitions.FileTypes.Images.All());
            resolver.GetDefinitions(FileType.Text).Should()
                .BeEquivalentTo(DefaultDefinitions.FileTypes.Text.All());
            resolver.GetDefinitions(FileType.Xml).Should()
                .BeEquivalentTo(DefaultDefinitions.FileTypes.Xml.All());
            resolver.GetDefinitions(FileType.All).Should()
                .BeEquivalentTo(DefaultDefinitions.All());
        }
    }
}
