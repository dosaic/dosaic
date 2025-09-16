using System.Reflection;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Extensions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class ObjectExtensionsTests
    {
        private BigClass _source;

        [SetUp]
        public void Up()
        {
            _source = new BigClass
            {
                Id = "123",
                Name = "test",
                Age = 19,
                Nested = new BigClassNested { Name = "nested", Id = "nested-id" },
                NestedList =
                [
                    new BigClassNested { Name = "nested0", Id = "nested-id0" },
                    new BigClassNested { Name = "nested1", Id = "nested-id1" }
                ],
                Tags = ["a", "b", "c"]
            };
        }

        [Test]
        public void PatchWorksForNormalProps()
        {
            var patch = new BigClass { Id = "124", Name = "patched", Age = 20 };
            _source.DeepPatch(patch);
            _source.Id.Should().Be("124");
            _source.Name.Should().Be("patched");
            _source.Age.Should().Be(20);
            _source.NextAge.Should().Be(21);
            _source.Nested.Id.Should().Be("nested-id");
            _source.Nested.Name.Should().Be("nested");
            _source.Tags.Should().HaveCount(3);
            _source.NestedList.Should().HaveCount(2);
        }

        [Test]
        public void PatchWorksWithLists()
        {
            var src = new List<int> { 1, 2, 5 };
            var patch = new List<int> { 1, 2, 3 };
            src.DeepPatch(patch);
            src.Should().HaveCount(4);
            src.Should().Contain([1, 2, 3, 5]);

            src = [1, 2, 5];
            src.DeepPatch(patch, PatchMode.IgnoreLists);
            src.Should().Contain([1, 2, 5]);

            var patchCls = new BigClass
            {
                Tags = ["abc"],
                NestedList =
                [
                    new BigClassNested { Name = "UPDATE", Id = "UPDATE" }
                ]
            };
            _source.DeepPatch(patchCls);
            _source.Tags.Should().HaveCount(4);
            _source.Tags.Should().Contain(["a", "b", "c", "abc"]);
            _source.NestedList.Should().HaveCount(3);
            _source.NestedList[0].Id.Should().Be("nested-id0");
            _source.NestedList[0].Name.Should().Be("nested0");
            _source.NestedList[1].Id.Should().Be("nested-id1");
            _source.NestedList[1].Name.Should().Be("nested1");
            _source.NestedList[2].Id.Should().Be("UPDATE");
            _source.NestedList[2].Name.Should().Be("UPDATE");

            _source.DeepPatch(patchCls, PatchMode.OverwriteLists);
            _source.Tags.Should().HaveCount(1);
            _source.Tags.Should().Contain(["abc"]);
            _source.NestedList.Should().HaveCount(1);
            _source.NestedList[0].Id.Should().Be("UPDATE");
            _source.NestedList[0].Name.Should().Be("UPDATE");

            IEnumerable<int> x = [];
            // ReSharper disable once PossibleMultipleEnumeration
            x.DeepPatch(null);
            // ReSharper disable once PossibleMultipleEnumeration
            x.Should().BeEmpty();
        }

        [Test]
        public void PatchWorksWithObjects()
        {
            var patch = new BigClass { Age = 25, Nested = new BigClassNested { Id = "patched-id", Name = "patched-nested" } };
            _source.DeepPatch(patch);
            _source.Id.Should().Be("123");
            _source.Name.Should().Be("test");
            _source.Age.Should().Be(25);
            _source.NextAge.Should().Be(26);
            _source.Nested.Id.Should().Be("patched-id");
            _source.Nested.Name.Should().Be("patched-nested");
            _source.Tags.Should().HaveCount(3);
            _source.NestedList.Should().HaveCount(2);
        }

        [Test]
        public void GetListValueDoesNothingOnInvalidEnumerable()
        {
            var method =
                typeof(ObjectExtensions).GetMethod("GetListValue", BindingFlags.Static | BindingFlags.NonPublic)!;
            var value = method.Invoke(null, [typeof(string), new Dictionary<string, string>(), new Dictionary<string, string>()]);
            value.Should().BeNull();
        }

        private class BigClass
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
            public int NextAge => Age + 1;
            public BigClassNested Nested { get; set; }
            public List<string> Tags { get; set; }
            public List<BigClassNested> NestedList { get; set; }
        }

        private class BigClassNested
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}
