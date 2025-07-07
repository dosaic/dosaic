using AwesomeAssertions;
using Chronos.Abstractions;
using Dosaic.Extensions.NanoIds;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Eventsourcing;

public class AggregateEventTests
{
    private IDateTimeProvider _dateTimeProvider;

    [SetUp]
    public void SetUp()
    {
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
    }

    [Test]
    public void PropertiesShouldBeInitializedCorrectly()
    {
        var id = NanoId.NewId<TestAggregate>();
        var eventData = "test data";
        var validFrom = _dateTimeProvider.UtcNow;
        var modifiedBy = NanoId.NewId<TestAggregate>();

        var testEvent = new TestAggregate
        {
            Id = id,
            EventData = eventData,
            ModifiedUtc = _dateTimeProvider.UtcNow,
            ValidFrom = validFrom,
            ModifiedBy = modifiedBy,
            TestProperty = "test",
            EventType = TestEventType.Update,
            IsDeleted = true
        };

        testEvent.Id.Should().Be(id);
        testEvent.EventData.Should().Be(eventData);
        testEvent.ValidFrom.Should().Be(validFrom);
        testEvent.ModifiedBy.Should().Be(modifiedBy);
        testEvent.EventType.Should().Be(TestEventType.Update);
        testEvent.IsDeleted.Should().BeTrue();
        testEvent.TestProperty.Should().Be("test");
        testEvent.ModifiedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public void GenericAggregateEventShouldSetEventTypeCorrectly()
    {
        var testEvent = new TestAggregate
        {
            Id = NanoId.NewId<TestAggregate>(),
            EventData = "test data",
            ValidFrom = _dateTimeProvider.UtcNow,
            EventType = TestEventType.Delete
        };

        testEvent.EventType.Should().Be(TestEventType.Delete);
    }
}
