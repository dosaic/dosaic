using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Abstractions.Attributes;
using Dosaic.Plugins.Jobs.TickerQ.Attributes;
using NUnit.Framework;
using TickerQ.Utilities.Enums;

namespace Dosaic.Plugins.Jobs.TickerQ.Tests
{
    public class AttributeTests
    {
        [Test]
        public void RecurringJobAttributeStoresCronPattern()
        {
            var attr = new RecurringJobAttribute("*/5 * * * *");
            attr.CronPattern.Should().Be("*/5 * * * *");
        }

        [Test]
        public void JobTimeoutAttributeConvertsSeconds()
        {
            var attr = new JobTimeoutAttribute(30, TimeUnit.Seconds);
            attr.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        }

        [Test]
        public void JobTimeoutAttributeConvertsMinutes()
        {
            var attr = new JobTimeoutAttribute(5, TimeUnit.Minutes);
            attr.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Test]
        public void JobTimeoutAttributeConvertsHours()
        {
            var attr = new JobTimeoutAttribute(2, TimeUnit.Hours);
            attr.Timeout.Should().Be(TimeSpan.FromHours(2));
        }

        [Test]
        public void JobTimeoutAttributeConvertsDays()
        {
            var attr = new JobTimeoutAttribute(1, TimeUnit.Days);
            attr.Timeout.Should().Be(TimeSpan.FromDays(1));
        }

        [Test]
        public void JobTimeoutAttributeConvertsMilliseconds()
        {
            var attr = new JobTimeoutAttribute(500, TimeUnit.Milliseconds);
            attr.Timeout.Should().Be(TimeSpan.FromMilliseconds(500));
        }

        [Test]
        public void JobPriorityAttributeStoresPriority()
        {
            var attr = new JobPriorityAttribute(TickerTaskPriority.High);
            attr.Priority.Should().Be(TickerTaskPriority.High);
        }

        [Test]
        public void JobTimeZoneAttributeStoresTimeZone()
        {
            var attr = new JobTimeZoneAttribute("Europe/Berlin");
            attr.TimeZoneInfo.Should().Be(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
        }
    }
}
