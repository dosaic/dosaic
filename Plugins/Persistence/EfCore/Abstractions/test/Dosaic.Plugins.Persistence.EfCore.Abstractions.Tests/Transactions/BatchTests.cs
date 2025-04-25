using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Transactions;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Transactions
{
    public class BatchTests
    {
        [Test]
        public void BatchWithSingleGenericParameterInitializesWithEmptyCollections()
        {
            var batch = new Batch<TestEntity>()
            {
                Add = new List<TestEntity>(),
                Delete = new List<NanoId>(),
                Update = new List<TestEntity>()
            };

            batch.Add.Should().NotBeNull();
            batch.Add.Should().BeEmpty();
            batch.Update.Should().NotBeNull();
            batch.Update.Should().BeEmpty();
            batch.Delete.Should().NotBeNull();
            batch.Delete.Should().BeEmpty();
        }

        [Test]
        public void BatchWithSingleGenericParameterCanAddItems()
        {
            var batch = new Batch<TestEntity> { Add = { new TestEntity { Id = "1" }, new TestEntity { Id = "2" } } };

            batch.Add.Should().HaveCount(2);
            batch.Add.Should().ContainSingle(e => e.Id == "1");
            batch.Add.Should().ContainSingle(e => e.Id == "2");
        }

        [Test]
        public void BatchWithSingleGenericParameterCanUpdateItems()
        {
            var batch = new Batch<TestEntity> { Update = { new TestEntity { Id = "1", Name = "Updated" } } };

            batch.Update.Should().HaveCount(1);
            batch.Update.First().Name.Should().Be("Updated");
        }

        [Test]
        public void BatchWithSingleGenericParameterCanDeleteItems()
        {
            var batch = new Batch<TestEntity> { Delete = { new NanoId("1"), new NanoId("2") } };

            batch.Delete.Should().HaveCount(2);
            batch.Delete.Should().Contain(id => id.ToString() == "1");
            batch.Delete.Should().Contain(id => id.ToString() == "2");
        }

        [Test]
        public void BatchWithTwoGenericParametersInitializesWithEmptyCollections()
        {
            var batch = new Batch<AddEntity, UpdateEntity>()
            {
                Add = new List<AddEntity>(),
                Delete = new List<NanoId>(),
                Update = new List<UpdateEntity>()
            };

            batch.Add.Should().NotBeNull();
            batch.Add.Should().BeEmpty();
            batch.Update.Should().NotBeNull();
            batch.Update.Should().BeEmpty();
            batch.Delete.Should().NotBeNull();
            batch.Delete.Should().BeEmpty();
        }

        [Test]
        public void BatchWithTwoGenericParametersCanAddItems()
        {
            var batch = new Batch<AddEntity, UpdateEntity>
            {
                Add = { new AddEntity { NewId = "1" }, new AddEntity { NewId = "2" } }
            };

            batch.Add.Should().HaveCount(2);
            batch.Add.Should().ContainSingle(e => e.NewId == "1");
            batch.Add.Should().ContainSingle(e => e.NewId == "2");
        }

        [Test]
        public void BatchWithTwoGenericParametersCanUpdateItems()
        {
            var batch = new Batch<AddEntity, UpdateEntity>
            {
                Update = { new UpdateEntity { ExistingId = "1", UpdatedName = "Updated" } }
            };

            batch.Update.Should().HaveCount(1);
            batch.Update.First().UpdatedName.Should().Be("Updated");
        }

        [Test]
        public void BatchWithTwoGenericParametersCanDeleteItems()
        {
            var batch = new Batch<AddEntity, UpdateEntity> { Delete = { new NanoId("1"), new NanoId("2") } };

            batch.Delete.Should().HaveCount(2);
            batch.Delete.Should().Contain(id => id.ToString() == "1");
            batch.Delete.Should().Contain(id => id.ToString() == "2");
        }

        private class TestEntity
        {
            public string Id { get; init; }
            public string Name { get; init; }
        }

        private class AddEntity
        {
            public string NewId { get; init; }
            public string NewName { get; init; }
        }

        private class UpdateEntity
        {
            public string ExistingId { get; init; }
            public string UpdatedName { get; init; }
        }
    }


}
