using Dosaic.Plugins.Validations.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests;

public class AttributeValidationPluginTests
{
    [Test]
    public void RegistersValidator()
    {
        var services = new ServiceCollection();
        new AttributeValidationPlugin().ConfigureServices(services);
        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IValidator>();
        validator.Should().NotBeNull();
    }
}
