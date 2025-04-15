using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Handlers.Cqrs.Tests
{
    public class IdentifierTests
    {
        [Test]
        public void GuidIdentifierNewShouldNotBeEqualToGuidIdentifierNew()
        {
            Identifier.New.Id.Should().NotBe(Identifier.New.Id);
        }

        [Test]
        public void GuidIdentifierParseShouldBeWorking()
        {
            Identifier.Parse("05a82a17-5a7f-4f28-8ff9-37f35c2cfb5f").Id.Should().Be("05a82a17-5a7f-4f28-8ff9-37f35c2cfb5f");
        }

        [Test]
        public void EmptyGuidIdentifierIsAnEmptyGuid()
        {
            var empty = Identifier.Empty;
            empty.Should().Be(Identifier.Empty);
            empty.Id.Should().Be(Guid.Empty);
        }

        [Test]
        public void CanResetTheIdOfAnGuidIdentifier()
        {
            var empty = Identifier.Empty;
            var newGuid = Guid.NewGuid();
            empty.Id = newGuid;
            empty.Id.Should().Be(newGuid);
        }
    }
}
