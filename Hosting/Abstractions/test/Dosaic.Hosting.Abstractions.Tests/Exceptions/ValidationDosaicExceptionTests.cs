using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Hosting.Abstractions.Middlewares.Models;
using FluentAssertions;
using NUnit.Framework;
#pragma warning disable SYSLIB0011

namespace Dosaic.Hosting.Abstractions.Tests.Exceptions
{
    public class ValidationDosaicExceptionTests
    {
        [Test]
        public void CanInitWithMessage()
        {
            var validationException = new ValidationDosaicException("test");
            validationException.HttpStatus.Should().Be(400);
            validationException.Message.Should().Be("test");
            validationException.ValidationErrors.Should().HaveCount(0);
        }

        [Test]
        public void CanInitWithMessageAndValidationErrors()
        {
            var validationException = new ValidationDosaicException("test", new List<FieldValidationError> { new("test", "test2") });
            validationException.HttpStatus.Should().Be(400);
            validationException.Message.Should().Be("test");
            validationException.ValidationErrors.Should().HaveCount(1);
            var validationError = validationException.ValidationErrors.Single();
            validationError.Field.Should().Be("test");
            validationError.ValidationMessage.Should().Be("test2");
        }
    }
}
