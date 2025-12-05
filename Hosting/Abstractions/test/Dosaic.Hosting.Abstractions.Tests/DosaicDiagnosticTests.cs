using System.Diagnostics;
using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests
{
    public class DosaicDiagnosticTests
    {
        private static readonly ActivitySource _source = DosaicDiagnostic.CreateSource();

        [Test]
        public void CreateSourceUsesTheCorrectName()
        {
            _source.Name.Should().Be(typeof(DosaicDiagnosticTests).FullName);
            var src = DosaicDiagnostic.CreateSource();
            src.Name.Should().Be(typeof(DosaicDiagnosticTests).FullName);
        }
    }
}
