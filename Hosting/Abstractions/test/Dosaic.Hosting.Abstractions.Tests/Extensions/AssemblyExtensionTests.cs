using System.Reflection;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Extensions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class AssemblyExtensionTests
    {
        [Test]
        public void CanQueryTypesEasily()
        {
            typeof(AssemblyExtensionTests).GetAssemblyTypesSafely(t => t.Name == nameof(AssemblyExtensionTests))
                .Should().HaveCount(1);
            var x = new List<Assembly> { typeof(AssemblyExtensionTests).Assembly }; ;
            x.GetTypesSafely(t => t.Name == nameof(AssemblyExtensionTests))
                .Should().HaveCount(1);
        }
    }
}
