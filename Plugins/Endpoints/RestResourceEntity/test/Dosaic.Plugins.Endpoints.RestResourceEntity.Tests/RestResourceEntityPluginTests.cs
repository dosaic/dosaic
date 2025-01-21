using NUnit.Framework;
using Dosaic.Testing.Assertions;

namespace Dosaic.Plugins.Endpoints.RestResourceEntity.Tests
{
    public class RestResourceEntityPluginTests
    {
        [Test]
        public void RegistersRequiredServices()
        {
            var plugin = new RestResourceEntityPlugin();
            plugin.ShouldHaveServices(new Dictionary<Type, Type>
            {
                {typeof(GlobalResponseOptions), null}
            });
        }
    }
}
