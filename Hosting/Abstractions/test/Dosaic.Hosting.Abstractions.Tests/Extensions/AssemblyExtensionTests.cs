using System.Reflection;
using Dosaic.Hosting.Abstractions.Extensions;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class AssemblyExtensionTests
    {
        [Test]
        public void CanQueryTypesEasily()
        {
            typeof(AssemblyExtensionTests).GetAssemblyTypes(t => t.Name == nameof(AssemblyExtensionTests))
                .Should().HaveCount(1);
            var x = new List<Assembly> { typeof(AssemblyExtensionTests).Assembly }; ;
            x.GetTypes(t => t.Name == nameof(AssemblyExtensionTests))
                .Should().HaveCount(1);
        }
    }
}
