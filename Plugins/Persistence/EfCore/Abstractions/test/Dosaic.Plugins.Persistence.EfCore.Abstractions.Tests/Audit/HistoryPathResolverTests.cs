using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryPathResolverTests
    {
        [Test]
        public void ResolvesSingleLevelChain()
        {
            var resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel)]);
            resolver.TryGet(typeof(TestHistoryChildModel), out var info).Should().BeTrue();
            info.RootType.Should().Be(typeof(TestHistoryModel));
            info.Links.Should().HaveCount(1);
            info.Links[0].ParentType.Should().Be(typeof(TestHistoryModel));
            info.Links[0].ParentIdProperty.Name.Should().Be(nameof(TestHistoryChildModel.ParentId));
            info.Links[0].CollectionOnParent.Name.Should().Be(nameof(TestHistoryModel.Children));
            info.Links[0].IsCollection.Should().BeTrue();
            resolver.GetChildren(typeof(TestHistoryModel)).Should().Contain(typeof(TestHistoryChildModel));
            resolver.AllChildTypes.Should().Contain(typeof(TestHistoryChildModel));
        }

        [Test]
        public void ResolvesMultiLevelChain()
        {
            var resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel), typeof(TestHistoryGrandchildModel)]);
            resolver.TryGet(typeof(TestHistoryGrandchildModel), out var info).Should().BeTrue();
            info.RootType.Should().Be(typeof(TestHistoryModel));
            info.Links.Should().HaveCount(2);
            info.Links[0].ParentType.Should().Be(typeof(TestHistoryChildModel));
            info.Links[1].ParentType.Should().Be(typeof(TestHistoryModel));
        }

        [Test]
        public void GetReturnsNullForUnknownType()
        {
            var resolver = HistoryPathResolver.Build([]);
            resolver.Get(typeof(TestHistoryChildModel)).Should().BeNull();
        }

        [Test]
        public void ThrowsWhenChainHasNoIHistoryRoot()
        {
            var act = () => HistoryPathResolver.Build([typeof(DanglingChild)]);
            act.Should().Throw<InvalidOperationException>().WithMessage("*[HistoryParent]*");
        }

        [Test]
        public void ThrowsOnDualRoleEntity()
        {
            var act = () => HistoryPathResolver.Build([typeof(DualRoleEntity)]);
            act.Should().Throw<InvalidOperationException>().WithMessage("*either a history root or a history child*");
        }

        [Test]
        public void ThrowsWhenParentIdPropertyMissing()
        {
            var act = () => HistoryPathResolver.Build([typeof(MissingFkChild)]);
            act.Should().Throw<InvalidOperationException>().WithMessage("*parent-id property*");
        }

        [Test]
        public void ThrowsWhenAmbiguousNavigation()
        {
            var act = () => HistoryPathResolver.Build([typeof(HistoryAmbiguousChild)]);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Ambiguous navigation*");
        }

        [Test]
        public void ThrowsWhenExplicitCollectionMissingOnParent()
        {
            var act = () => HistoryPathResolver.Build([typeof(BadCollectionChild)]);
            act.Should().Throw<InvalidOperationException>().WithMessage("*has no property*");
        }

        [Test]
        public void ThrowsWhenExplicitCollectionDoesNotTargetChildType()
        {
            var act = () => HistoryPathResolver.Build([typeof(WrongTypeCollectionChild)]);
            act.Should().Throw<InvalidOperationException>().WithMessage("*does not target*");
        }

        [Test]
        public void ResolveRootIdSingleLevel()
        {
            var resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel)]);
            var info = resolver.Get(typeof(TestHistoryChildModel));
            NanoId rootId = "ROOT";
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = rootId };
            info.ResolveRootId(child, null, (_, _) => null).Should().Be(rootId);
        }

        [Test]
        public void BuildPathSingleLevel()
        {
            var resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel)]);
            var info = resolver.Get(typeof(TestHistoryChildModel));
            NanoId rootId = "ROOT";
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = rootId };
            info.BuildPath(child, null, (_, _) => null).Should().Be($"{nameof(TestHistoryModel.Children)}.CH1");
        }

        [Test]
        public void BuildPathMultiLevelUsesParentLookup()
        {
            var resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel), typeof(TestHistoryGrandchildModel)]);
            var info = resolver.Get(typeof(TestHistoryGrandchildModel));
            NanoId rootId = "ROOT";
            var parent = new TestHistoryChildModel { Id = "CH1", ParentId = rootId };
            var grandchild = new TestHistoryGrandchildModel { Id = "G1", ChildId = parent.Id };
            object Lookup(Type t, NanoId id) => t == typeof(TestHistoryChildModel) && id.Equals(parent.Id) ? parent : null;
            info.BuildPath(grandchild, null, Lookup).Should().Be("Children.CH1.Parts.G1");
            info.ResolveRootId(grandchild, null, Lookup).Should().Be(rootId);
        }

        [Test]
        public void BuildPathThrowsWhenParentLookupReturnsNull()
        {
            var resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel), typeof(TestHistoryGrandchildModel)]);
            var info = resolver.Get(typeof(TestHistoryGrandchildModel));
            var grandchild = new TestHistoryGrandchildModel { Id = "G1", ChildId = "MISSING" };
            var act = () => info.BuildPath(grandchild, null, (_, _) => null);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Could not resolve parent*");
        }

        [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
        [HistoryParent(typeof(DanglingParent), nameof(ParentId))]
        public class DanglingChild : Model
        {
            public NanoId ParentId { get; set; }
        }

        public class DanglingParent : Model
        {
            public string Name { get; set; }
        }

        [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
        [HistoryParent(typeof(DualRoleParent), nameof(ParentId), Collection = nameof(DualRoleParent.Items))]
        public class DualRoleEntity : Model, IHistory
        {
            public NanoId ParentId { get; set; }
        }

        public class DualRoleParent : Model, IHistory
        {
            public ICollection<DualRoleEntity> Items { get; set; }
        }

        [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
        [HistoryParent(typeof(TestHistoryModel), "DoesNotExist")]
        public class MissingFkChild : Model
        {
        }

        [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
        [HistoryParent(typeof(HistoryAmbiguousParent), nameof(ParentId))]
        public class HistoryAmbiguousChild : Model
        {
            public NanoId ParentId { get; set; }
        }

        public class HistoryAmbiguousParent : Model, IHistory
        {
            public ICollection<HistoryAmbiguousChild> ChildrenA { get; set; }
            public ICollection<HistoryAmbiguousChild> ChildrenB { get; set; }
        }

        [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
        [HistoryParent(typeof(TestHistoryModel), nameof(ParentId), Collection = "NotARealProperty")]
        public class BadCollectionChild : Model
        {
            public NanoId ParentId { get; set; }
        }

        [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
        [HistoryParent(typeof(TestHistoryModel), nameof(ParentId), Collection = nameof(TestHistoryModel.HistoryProperty))]
        public class WrongTypeCollectionChild : Model
        {
            public NanoId ParentId { get; set; }
        }
    }
}
