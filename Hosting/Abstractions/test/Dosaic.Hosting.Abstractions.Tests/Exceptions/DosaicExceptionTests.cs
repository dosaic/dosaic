using Dosaic.Hosting.Abstractions.Exceptions;
using FluentAssertions;
using NUnit.Framework;
#pragma warning disable SYSLIB0011

namespace Dosaic.Hosting.Abstractions.Tests.Exceptions
{
    public class DosaicExceptionTests
    {
        [Test]
        public void CanInitEmpty()
        {
            var exception = new DosaicException();
            exception.Message.Should().Be("An unhandled error occured.");
            exception.HttpStatus.Should().Be(500);
        }

        [Test]
        public void CanInitWithMessage()
        {
            var exception = new DosaicException("test");
            exception.Message.Should().Be("test");
            exception.HttpStatus.Should().Be(500);
        }

        [Test]
        public void CanInitWithMessageAndException()
        {
            var exception = new DosaicException("test", new Exception("inner"));
            exception.Message.Should().Be("test");
            exception.HttpStatus.Should().Be(500);
            exception.InnerException!.Message.Should().Be("inner");
        }

        [Test]
        public void CanInitWithStatus()
        {
            var exception = new DosaicException(400);
            exception.Message.Should().Be("An unhandled error occured.");
            exception.HttpStatus.Should().Be(400);
        }

        [Test]
        public void CanInitWithMessageAndStatus()
        {
            var exception = new DosaicException("test", 400);
            exception.Message.Should().Be("test");
            exception.HttpStatus.Should().Be(400);
        }

        [Test]
        public void CanInitWithMessageAndStatusAndException()
        {
            var exception = new DosaicException("test", 400, new Exception("inner"));
            exception.Message.Should().Be("test");
            exception.HttpStatus.Should().Be(400);
            exception.InnerException!.Message.Should().Be("inner");
        }
    }
}
