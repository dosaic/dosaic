using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Models
{
    public class ModelTests
    {
        [Test]
        public void ModelIdGetSetShouldWork()
        {
            var model = new TestHistoryModel { Id = "123" };
            model.Id.Should().Be("123");
            model.Id = "456";
            model.Id.Should().Be("456");
        }
    }
}
