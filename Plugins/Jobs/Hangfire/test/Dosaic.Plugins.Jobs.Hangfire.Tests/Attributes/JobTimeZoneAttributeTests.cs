using FluentAssertions;
using NUnit.Framework;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Attributes
{
    public class JobTimeZoneAttributeTests
    {
        [Test]
        public void CanInitializeWithTimeZone()
        {
            var timeZoneAttribute = new JobTimeZoneAttribute(TimeZoneInfo.Utc);
            timeZoneAttribute.TimeZoneInfo.Should().Be(TimeZoneInfo.Utc);

            timeZoneAttribute = new JobTimeZoneAttribute(TimeZoneInfo.Local);
            timeZoneAttribute.TimeZoneInfo.Should().Be(TimeZoneInfo.Local);
        }
    }
}
