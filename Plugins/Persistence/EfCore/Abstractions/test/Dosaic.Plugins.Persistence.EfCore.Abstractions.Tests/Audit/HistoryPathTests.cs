using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryPathTests
    {
        private static HistoryPathInfo SingleLevel() =>
            HistoryPathResolver.Build([typeof(TestHistoryChildModel)]).Get(typeof(TestHistoryChildModel));

        private static HistoryPathInfo MultiLevel() =>
            HistoryPathResolver.Build([typeof(TestHistoryChildModel), typeof(TestHistoryGrandchildModel)])
                .Get(typeof(TestHistoryGrandchildModel));

        [Test]
        public void ResolveRootIdThrowsWhenEntityAndPreviousAreBothNull()
        {
            var info = SingleLevel();
            var act = () => info.ResolveRootId(null, null, (_, _) => null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void BuildPathThrowsWhenEntityAndPreviousAreBothNull()
        {
            var info = SingleLevel();
            var act = () => info.BuildPath(null, null, (_, _) => null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void ResolveRootIdThrowsWhenParentLookupReturnsNull()
        {
            var info = MultiLevel();
            var grand = new TestHistoryGrandchildModel { Id = "G1", ChildId = "MISSING" };
            var act = () => info.ResolveRootId(grand, null, (_, _) => null);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Could not resolve parent*");
        }

        [Test]
        public void BuildPathThrowsWhenParentLookupReturnsNull()
        {
            var info = MultiLevel();
            var grand = new TestHistoryGrandchildModel { Id = "G1", ChildId = "MISSING" };
            var act = () => info.BuildPath(grand, null, (_, _) => null);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Could not resolve parent*");
        }

        [Test]
        public void BuildPathUsesPreviousEntityWhenEntityIsNull()
        {
            var info = SingleLevel();
            NanoId rootId = "ROOT";
            var previous = new TestHistoryChildModel { Id = "CH1", ParentId = rootId };
            info.BuildPath(null, previous, (_, _) => null)
                .Should().Be($"{nameof(TestHistoryModel.Children)}.CH1");
            info.ResolveRootId(null, previous, (_, _) => null).Should().Be(rootId);
        }

        [Test]
        public void BuildPathWithEmptyLinksReturnsEmptyString()
        {
            var info = new HistoryPathInfo { RootType = typeof(TestHistoryModel) };
            var entity = new TestHistoryModel { Id = "R1", HistoryProperty = "x" };
            info.BuildPath(entity, null, (_, _) => null).Should().BeEmpty();
            info.ResolveRootId(entity, null, (_, _) => null).Should().Be(default(NanoId));
        }
    }
}
