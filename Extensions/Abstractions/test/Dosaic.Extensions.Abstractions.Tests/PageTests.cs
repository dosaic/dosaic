using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Extensions.Abstractions.Tests
{
    [TestFixture]
    public class PageTests
    {
        [Test]
        public void DefaultConstructorInitializesProperties()
        {
            var page = new Page();

            page.Current.Should().Be(0);
            page.Size.Should().BeNull();
            page.TotalElements.Should().Be(0);
            page.TotalPages.Should().Be(0);
        }

        [Test]
        public void ParameterizedConstructorSetsPropertiesCorrectly()
        {
            var page = new Page(10, 2, 100, 10);

            page.Size.Should().Be(10);
            page.Current.Should().Be(2);
            page.TotalElements.Should().Be(100);
            page.TotalPages.Should().Be(10);
        }

        [Test]
        public void ParameterizedConstructorHandlesNullSizeCorrectly()
        {
            var page = new Page(null, 2, 100, 10);

            page.Size.Should().Be(int.MaxValue);
        }

        [Test]
        public void ParameterizedConstructorHandlesNullCurrentCorrectly()
        {
            var page = new Page(10, null, 100, 10);

            page.Current.Should().Be(0);
        }

        [Test]
        public void NumberPropertyReturnsCurrentValue()
        {
            var page = new Page { Current = 5 };

            page.Number.Should().Be(5);
        }

        [Test]
        public void SettingNumberPropertyUpdatesCurrent()
        {
            var page = new Page();
            page.Number = 5;

            page.Current.Should().Be(5);
        }

        [Test]
        public void SettingNullNumberDefaultsToZero()
        {
            var page = new Page();
            page.Number = null;

            page.Number.Should().Be(0);
        }

        [Test]
        public void SettingNullCurrentDefaultsToZero()
        {
            var page = new Page();
            page.Current = null;

            page.Current.Should().Be(0);
        }
    }
}
