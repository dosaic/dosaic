using FluentAssertions;
using NUnit.Framework;
using Dosaic.Hosting.Abstractions.Exceptions;
#pragma warning disable SYSLIB0011

namespace Dosaic.Hosting.Abstractions.Tests.Exceptions
{
    public class ConflictDosaicExceptionTests
    {
        [Test]
        public void CanInitWithMessage()
        {
            var validationException = new ConflictDosaicException("test");
            validationException.HttpStatus.Should().Be(409);
            validationException.Message.Should().Be("test");
        }
    }
}
