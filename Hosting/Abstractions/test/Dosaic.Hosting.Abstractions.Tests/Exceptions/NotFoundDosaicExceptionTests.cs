using FluentAssertions;
using NUnit.Framework;
using Dosaic.Hosting.Abstractions.Exceptions;
#pragma warning disable SYSLIB0011

namespace Dosaic.Hosting.Abstractions.Tests.Exceptions
{
    public class NotFoundDosaicExceptionTests
    {
        [Test]
        public void CanInitWithMessage()
        {
            var validationException = new NotFoundDosaicException("test");
            validationException.HttpStatus.Should().Be(404);
            validationException.Message.Should().Be("test");
        }

        [Test]
        public void CanInitWithEntityAndId()
        {
            var validationException = new NotFoundDosaicException("test", "123");
            validationException.HttpStatus.Should().Be(404);
            validationException.Message.Should().Be("Could not find test with id '123'");
        }

    }
}
