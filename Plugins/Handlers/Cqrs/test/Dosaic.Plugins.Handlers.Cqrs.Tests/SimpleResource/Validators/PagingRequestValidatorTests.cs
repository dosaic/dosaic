using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using Dosaic.Extensions.Abstractions;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators;

namespace Dosaic.Plugins.Handlers.Cqrs.Tests.SimpleResource.Validators
{
    public class PagingRequestValidatorTests
    {
        private readonly IValidator _validator = new GenericValidator<PagingRequest>(PagingRequestValidator.Validate);

        private static ValidationContext<T> GetContext<T>(T obj) => new(obj);

        [Test]
        [TestCase(null, null, null, null, true)]
        [TestCase(0, null, null, null, false)]
        [TestCase(-1, null, null, null, false)]
        [TestCase(null, -1, null, null, false)]
        [TestCase(null, null, "", null, false)]
        [TestCase(null, null, null, "", false)]
        [TestCase(101, null, null, null, false)]
        [TestCase(100, 0, "Name eq 'test'", "Name asc", true)]
        public void PagingRequestValidatorWorks(int? size, int? page, string filter, string sort, bool isValid)
        {
            var pagingRequest = new PagingRequest(size, page, filter, sort);
            var result = _validator.Validate(GetContext(pagingRequest));
            result.IsValid.Should().Be(isValid);
        }
    }
}
