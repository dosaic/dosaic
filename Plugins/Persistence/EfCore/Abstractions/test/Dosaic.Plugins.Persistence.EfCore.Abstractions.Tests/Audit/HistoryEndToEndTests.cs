using AwesomeAssertions;
using Chronos.Abstractions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryEndToEndTests
    {
        private TestEfCoreDb _db;
        private IServiceProvider _scopeSp;
        private HistoryPathResolver _resolver;
        private IDateTimeProvider _clock;
        private DateTime _now;

        [SetUp]
        public void Setup()
        {
            _now = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel), typeof(TestHistoryGrandchildModel)]);

            var sp = Substitute.For<IServiceProvider>();
            _scopeSp = Substitute.For<IServiceProvider>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            sp.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(_scopeSp);
            scopeFactory.CreateScope().Returns(scope);

            var userProvider = Substitute.For<IUserIdProvider>();
            userProvider.IsUserInteraction.Returns(true);
            userProvider.UserId.Returns("U1");
            userProvider.FallbackUserId.Returns("sys");

            _clock = Substitute.For<IDateTimeProvider>();
            _clock.UtcNow.Returns(_ => _now);

            var writer = new HistoryWriter(userProvider, _clock, _resolver);
            _scopeSp.GetService(typeof(IHistoryWriter)).Returns(writer);

            var opts = new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .UseApplicationServiceProvider(sp)
                .Options;
            _db = new TestEfCoreDb(opts);
        }

        [TearDown]
        public void TearDown() => _db?.Dispose();

        [Test]
        public async Task AddRootProducesOneHistoryRow()
        {
            var root = new TestHistoryModel { Id = "R1", HistoryProperty = "alpha" };
            _db.Set<TestHistoryModel>().Add(root);
            await _db.SaveChangesAsync();
            var rows = _db.Set<History<TestHistoryModel>>().ToList();
            rows.Should().HaveCount(1);
            rows[0].ForeignId.Should().Be((NanoId)"R1");
            var changes = rows[0].GetChanges();
            changes.Should().ContainKey(nameof(TestHistoryModel.HistoryProperty));
            changes.Should().NotContainKey(nameof(TestHistoryModel.Children));
        }

        [Test]
        public async Task RootAndChildrenBundledIntoSingleRow()
        {
            var root = new TestHistoryModel { Id = "R1", HistoryProperty = "alpha" };
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = "R1", ChildName = "item", Price = 5m };
            var grand = new TestHistoryGrandchildModel { Id = "G1", ChildId = "CH1", PartName = "part", Qty = 2 };
            _db.Set<TestHistoryModel>().Add(root);
            _db.Set<TestHistoryChildModel>().Add(child);
            _db.Set<TestHistoryGrandchildModel>().Add(grand);
            await _db.SaveChangesAsync();

            var rows = _db.Set<History<TestHistoryModel>>().ToList();
            rows.Should().HaveCount(1);
            var changes = rows[0].GetChanges();
            changes.Should().ContainKey(nameof(TestHistoryModel.HistoryProperty));
            changes.Should().ContainKey("Children.CH1");
            changes.Should().ContainKey("Children.CH1.Parts.G1");
        }

        [Test]
        public async Task ChildOnlyChangeStillProducesRootRow()
        {
            var root = new TestHistoryModel { Id = "R1", HistoryProperty = "alpha" };
            _db.Set<TestHistoryModel>().Add(root);
            await _db.SaveChangesAsync();
            _db.Set<History<TestHistoryModel>>().Should().HaveCount(1);

            _now = _now.AddMinutes(1);
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = "R1", ChildName = "item", Price = 1m };
            _db.Set<TestHistoryChildModel>().Add(child);
            await _db.SaveChangesAsync();

            var rows = _db.Set<History<TestHistoryModel>>().OrderBy(r => r.ModifiedUtc).ToList();
            rows.Should().HaveCount(2);
            var childRow = rows[1].GetChanges();
            childRow.Should().ContainKey("Children.CH1");
            childRow.Should().NotContainKey(nameof(TestHistoryModel.HistoryProperty));
        }

        [Test]
        public async Task LoadFromHistoryReconstructsAtDate()
        {
            var root = new TestHistoryModel { Id = "R1", HistoryProperty = "alpha" };
            _db.Set<TestHistoryModel>().Add(root);
            await _db.SaveChangesAsync();
            var t0 = _now;

            _now = _now.AddMinutes(1);
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = "R1", ChildName = "item", Price = 1m };
            _db.Set<TestHistoryChildModel>().Add(child);
            await _db.SaveChangesAsync();
            var t1 = _now;

            _now = _now.AddMinutes(1);
            var tracked = _db.Set<TestHistoryChildModel>().Single(c => c.Id == (NanoId)"CH1");
            tracked.Price = 9m;
            await _db.SaveChangesAsync();
            var t2 = _now;

            var atT0 = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", t0);
            atT0.Should().NotBeNull();
            atT0.HistoryProperty.Should().Be("alpha");
            (atT0.Children ?? new List<TestHistoryChildModel>()).Should().BeEmpty();

            var atT1 = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", t1);
            atT1.Children.Should().HaveCount(1);
            atT1.Children.Single().Price.Should().Be(1m);

            var atT2 = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", t2);
            atT2.Children.Single().Price.Should().Be(9m);
        }

        [Test]
        public async Task LoadFromHistoryReturnsNullBeforeCreation()
        {
            var result = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", _now);
            result.Should().BeNull();
        }

        [Test]
        public async Task FullThreeLevelLifecycleReconstructsCorrectlyAtEachDate()
        {
            // t0: create root + 2 children, child CH1 has 2 parts
            var root = new TestHistoryModel { Id = "R1", HistoryProperty = "v1" };
            var ch1 = new TestHistoryChildModel { Id = "CH1", ParentId = "R1", ChildName = "ItemA", Price = 10m };
            var ch2 = new TestHistoryChildModel { Id = "CH2", ParentId = "R1", ChildName = "ItemB", Price = 20m };
            var p1 = new TestHistoryGrandchildModel { Id = "P1", ChildId = "CH1", PartName = "PartA", Qty = 1 };
            var p2 = new TestHistoryGrandchildModel { Id = "P2", ChildId = "CH1", PartName = "PartB", Qty = 2 };
            _db.Set<TestHistoryModel>().Add(root);
            _db.Set<TestHistoryChildModel>().AddRange(ch1, ch2);
            _db.Set<TestHistoryGrandchildModel>().AddRange(p1, p2);
            await _db.SaveChangesAsync();
            var t0 = _now;

            // t1: rename root, modify grandchild qty, add new part P3
            _now = _now.AddMinutes(1);
            var rootTracked = _db.Set<TestHistoryModel>().Single(x => x.Id == (NanoId)"R1");
            rootTracked.HistoryProperty = "v2";
            var p1Tracked = _db.Set<TestHistoryGrandchildModel>().Single(x => x.Id == (NanoId)"P1");
            p1Tracked.Qty = 99;
            _db.Set<TestHistoryGrandchildModel>().Add(new TestHistoryGrandchildModel { Id = "P3", ChildId = "CH1", PartName = "PartC", Qty = 3 });
            await _db.SaveChangesAsync();
            var t1 = _now;

            // t2: delete child CH2 entirely
            _now = _now.AddMinutes(1);
            var ch2Tracked = _db.Set<TestHistoryChildModel>().Single(x => x.Id == (NanoId)"CH2");
            _db.Set<TestHistoryChildModel>().Remove(ch2Tracked);
            await _db.SaveChangesAsync();
            var t2 = _now;

            // t3: delete part P2
            _now = _now.AddMinutes(1);
            var p2Tracked = _db.Set<TestHistoryGrandchildModel>().Single(x => x.Id == (NanoId)"P2");
            _db.Set<TestHistoryGrandchildModel>().Remove(p2Tracked);
            await _db.SaveChangesAsync();
            var t3 = _now;

            // verify each row stored
            _db.Set<History<TestHistoryModel>>().Should().HaveCount(4);

            // reconstruct @ t0
            var atT0 = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", t0);
            atT0.HistoryProperty.Should().Be("v1");
            atT0.Children.Should().HaveCount(2);
            var t0Ch1 = atT0.Children.Single(c => c.Id == (NanoId)"CH1");
            t0Ch1.ChildName.Should().Be("ItemA");
            t0Ch1.Price.Should().Be(10m);
            t0Ch1.Parts.Should().HaveCount(2);
            t0Ch1.Parts.Single(p => p.Id == (NanoId)"P1").Qty.Should().Be(1);

            // reconstruct @ t1
            var atT1 = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", t1);
            atT1.HistoryProperty.Should().Be("v2");
            atT1.Children.Should().HaveCount(2);
            var t1Ch1 = atT1.Children.Single(c => c.Id == (NanoId)"CH1");
            t1Ch1.Parts.Should().HaveCount(3);
            t1Ch1.Parts.Single(p => p.Id == (NanoId)"P1").Qty.Should().Be(99);
            t1Ch1.Parts.Should().Contain(p => p.Id == (NanoId)"P3");

            // reconstruct @ t2 - CH2 gone
            var atT2 = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", t2);
            atT2.Children.Should().HaveCount(1);
            atT2.Children.Single().Id.Should().Be((NanoId)"CH1");
            atT2.Children.Single().Parts.Should().HaveCount(3);

            // reconstruct @ t3 - P2 gone
            var atT3 = await _db.Set<TestHistoryModel>().LoadFromHistoryAsync("R1", t3);
            atT3.Children.Should().HaveCount(1);
            var finalCh1 = atT3.Children.Single();
            finalCh1.Parts.Should().HaveCount(2);
            finalCh1.Parts.Select(p => p.Id.Value).Should().BeEquivalentTo(["P1", "P3"]);
        }

        [Test]
        public async Task LoadHistoryTimelineReturnsAllVersions()
        {
            var root = new TestHistoryModel { Id = "R1", HistoryProperty = "alpha" };
            _db.Set<TestHistoryModel>().Add(root);
            await _db.SaveChangesAsync();

            _now = _now.AddMinutes(1);
            var tracked = _db.Set<TestHistoryModel>().Single(r => r.Id == (NanoId)"R1");
            tracked.HistoryProperty = "beta";
            await _db.SaveChangesAsync();

            var timeline = await _db.Set<TestHistoryModel>().LoadHistoryTimelineAsync("R1");
            timeline.Should().HaveCount(2);
            timeline[0].Snapshot.HistoryProperty.Should().Be("alpha");
            timeline[1].Snapshot.HistoryProperty.Should().Be("beta");
        }
    }
}
