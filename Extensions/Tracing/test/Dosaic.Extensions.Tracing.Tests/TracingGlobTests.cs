using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Extensions.Tracing.Tests
{
    [TestFixture]
    public class TracingGlobTests
    {
        [Test]
        public void ExactPatternMatchesOnlyExactName()
        {
            TracingGlob.IsMatch("MyApp.Services.Foo", "MyApp.Services.Foo").Should().BeTrue();
            TracingGlob.IsMatch("MyApp.Services.Bar", "MyApp.Services.Foo").Should().BeFalse();
        }

        [Test]
        public void SingleStarMatchesOneSegmentButNotAcrossDots()
        {
            TracingGlob.IsMatch("MyApp.Foo", "MyApp.*").Should().BeTrue();
            TracingGlob.IsMatch("MyApp.Foo.Bar", "MyApp.*").Should().BeFalse();
        }

        [Test]
        public void DoubleStarMatchesAcrossDots()
        {
            TracingGlob.IsMatch("MyApp.Foo.Bar.Baz", "MyApp.**").Should().BeTrue();
            TracingGlob.IsMatch("MyApp.Foo", "MyApp.**").Should().BeTrue();
        }

        [Test]
        public void SingleStarTrailingMatchesOneSegmentDeepOnly()
        {
            TracingGlob.IsMatch("MyApp.Migrations", "*.Migrations").Should().BeTrue();
            TracingGlob.IsMatch("MyApp.Data.Migrations", "*.Migrations").Should().BeFalse();
        }

        [Test]
        public void DoubleStarTrailingMatchesAnyDepth()
        {
            TracingGlob.IsMatch("MyApp.Migrations", "**.Migrations").Should().BeTrue();
            TracingGlob.IsMatch("MyApp.Data.Nested.Migrations", "**.Migrations").Should().BeTrue();
            TracingGlob.IsMatch("MyApp.Data.Other", "**.Migrations").Should().BeFalse();
        }

        [Test]
        public void NullOrEmptyIncludesMatchesEverything()
        {
            TracingGlob.Matches("Anything.At.All", null, null).Should().BeTrue();
            TracingGlob.Matches("Anything.At.All", Array.Empty<string>(), null).Should().BeTrue();
        }

        [Test]
        public void IncludeRestrictsToMatchingNames()
        {
            var includes = new[] { "MyApp.Services.*" };
            TracingGlob.Matches("MyApp.Services.Foo", includes, null).Should().BeTrue();
            TracingGlob.Matches("MyApp.Data.Foo", includes, null).Should().BeFalse();
        }

        [Test]
        public void ExcludeTakesPrecedenceOverInclude()
        {
            var includes = new[] { "MyApp.**" };
            var excludes = new[] { "**.Generated" };
            TracingGlob.Matches("MyApp.Services.Generated", includes, excludes).Should().BeFalse();
            TracingGlob.Matches("MyApp.Services.Real", includes, excludes).Should().BeTrue();
        }

        [Test]
        public void MultipleIncludeGlobsAreOred()
        {
            var includes = new[] { "MyApp.Features.*", "MyApp.Services.*" };
            TracingGlob.Matches("MyApp.Features.Foo", includes, null).Should().BeTrue();
            TracingGlob.Matches("MyApp.Services.Bar", includes, null).Should().BeTrue();
            TracingGlob.Matches("MyApp.Data.Baz", includes, null).Should().BeFalse();
        }

        [Test]
        public void SpecialRegexCharactersInNamesAreEscaped()
        {
            TracingGlob.IsMatch("MyApp.Foo+Inner", "MyApp.Foo+Inner").Should().BeTrue();
            TracingGlob.IsMatch("MyAppXFoo", "MyApp.Foo").Should().BeFalse();
        }
    }
}
