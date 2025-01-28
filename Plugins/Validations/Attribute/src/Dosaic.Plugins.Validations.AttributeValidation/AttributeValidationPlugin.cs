using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Validations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Validations.AttributeValidation;

public class AttributeValidationPlugin : IPluginServiceConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IValidator, AttributeValidator>();
    }
}
