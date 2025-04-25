using Dosaic.Hosting.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Testing.NUnit.Assertions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        // [Test]
        // public void CanAddDbContextHealthCheck()
        // {
        //     var hc = Substitute.For<IHealthChecksBuilder>();
        //     hc.AddEfContext<TestEfCoreDb>();
        //     hc.Received().Add(Arg.Is<HealthCheckRegistration>(h =>
        //         h.Name == nameof(TestEfCoreDb) && h.Tags.Contains(HealthCheckTag.Readiness.Value)));
        // }

        [Test]
        public void MigratesRelationalDatabases()
        {
            var opts = new DbContextOptionsBuilder<EfCoreDbContext>();
            opts.UseSqlite($"Data Source=./test-${Guid.NewGuid():N}.db");
            DbContext context = new TestEfCoreDb(opts.Options);
            context.Invoking(x => x.Database.Migrate()).Should().NotThrow();
        }

        [Test]
        public void CanMigrateAllContexts()
        {
            var sp = Substitute.For<IServiceProvider>();
            var opts = new DbContextOptionsBuilder<EfCoreDbContext>();
            opts.UseSqlite($"Data Source=./test-${Guid.NewGuid():N}.db");
            DbContext dbContext = new TestEfCoreDb(opts.Options);
            sp.GetService(typeof(IEnumerable<DbContext>)).Returns(new List<DbContext> { dbContext });
            var fakeLogger = new FakeLogger<EfCorePlugin>();
            sp.GetService(typeof(ILogger<EfCorePlugin>)).Returns(fakeLogger);
            var appBuilder = Substitute.For<IApplicationBuilder>();
            appBuilder.ApplicationServices.Returns(sp);
            var invoke = () =>
            appBuilder.MigrateEfContexts<DbContext>();
            invoke();
            appBuilder.ApplicationServices.Received(1).GetService(typeof(IEnumerable<DbContext>));
            fakeLogger.Entries[0].Message.Should().Be("Migrating 'TestEfCoreDb'");
            fakeLogger.Entries[1].Message.Should().Be("Migrated 'TestEfCoreDb'");
            invoke.Should().NotThrow();

        }
    }
}
