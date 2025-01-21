using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Endpoints.Abstractions.Tests
{
    public class ResourceIdentifierAttributeTests
    {
        [Test]
        public void CanInit()
        {
            var attr = new ResourceIdentifierAttribute();
            attr.Should().NotBeNull();
        }
    }
}
