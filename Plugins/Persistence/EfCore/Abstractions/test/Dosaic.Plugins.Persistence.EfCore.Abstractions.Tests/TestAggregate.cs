using System.Collections.Immutable;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    public enum TestEventType
    {
        Create,
        Update,
        Delete
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class NoMatcherAggregateEvent : AggregateEvent
    {
        public string GroupId { get; set; }
    }

    public class TestAggregateEventEventProcessor
        : IEventProcessor<TestAggregate>
    {
        public Task ProcessEventsAsync(IDb db, ImmutableArray<TestAggregate> events,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class TestAggregate : AggregateEvent<TestEventType>
    {
        [EventMatcher] public string GroupId { get; set; }

        public string TestProperty { get; set; }

        public static TestAggregate Init(DateTime validFrom, string groupId, NanoId id = null,
            string eventData = "test data",
            NanoId modifiedBy = null,
            string testPropertyValue = "test", TestEventType eventType = TestEventType.Update, bool isDeleted = false)
        {
            if (id == null)
                id = NanoId.NewId<TestAggregate>();

            if (modifiedBy == null)
                modifiedBy = NanoId.NewId<TestAggregate>();

            return new TestAggregate
            {
                Id = id,
                GroupId = groupId,
                EventData = eventData,
                ValidFrom = validFrom,
                ModifiedBy = modifiedBy,
                TestProperty = testPropertyValue,
                EventType = eventType,
                IsDeleted = isDeleted
            };
        }
    }

    internal class TestAggregateModelConfiguration : IEntityTypeConfiguration<TestAggregate>
    {
        public void Configure(EntityTypeBuilder<TestAggregate> builder)
        {
            builder.ToTable("testAggregate", "test");
            builder.Property(x => x.GroupId).HasMaxLength(64);
            builder.Property(x => x.EventData).HasMaxLength(64);
            builder.Property(x => x.TestProperty).HasMaxLength(64);
        }
    }

    internal class NoMatcherAggregateEventModelConfiguration : IEntityTypeConfiguration<NoMatcherAggregateEvent>
    {
        public void Configure(EntityTypeBuilder<NoMatcherAggregateEvent> builder)
        {
            builder.ToTable("noMatcherAggregate", "test");
            builder.Property(x => x.GroupId).HasMaxLength(64);
            builder.Property(x => x.EventData).HasMaxLength(64);
        }
    }
}
