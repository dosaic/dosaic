using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Uniqueness
{
    public class JobUniquenessStoresTests
    {
        [Test]
        public void UnknownStoragesFallBackToTheStorageAgnosticStore()
        {
            JobUniquenessStores.For(Substitute.For<JobStorage>()).Should().BeOfType<StorageJobUniquenessStore>();
        }

        [Test]
        public void RegisteredStoresAreResolvedPerStorage()
        {
            var storage = Substitute.For<JobStorage>();
            var other = Substitute.For<JobStorage>();
            var store = Substitute.For<IJobUniquenessStore>();
            JobUniquenessStores.Use(storage, store);

            JobUniquenessStores.For(storage).Should().BeSameAs(store);
            JobUniquenessStores.For(other).Should().NotBeSameAs(store);
        }
    }
}
