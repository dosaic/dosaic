using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    public class PostgresSchemaGuardTests
    {
        [TestCase("hangfire")]
        [TestCase("_hangfire2")]
        public void PlainIdentifiersArePassedThrough(string schema) =>
            PostgresSchemaGuard.ValidateName(schema).Should().Be(schema);

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("pub\"lic")]
        [TestCase("hang fire")]
        [TestCase("2hangfire")]
        [TestCase("hangfire; DROP SCHEMA public")]
        public void NamesThatWouldBreakOutOfAQuotedIdentifierAreRejected(string schema)
        {
            var act = () => PostgresSchemaGuard.ValidateName(schema);
            act.Should().Throw<ArgumentException>();
        }
    }
}
