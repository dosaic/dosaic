using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Uniqueness
{
    public class PostgresJobUniquenessStoreTests
    {
        private static readonly string _sql = PostgresJobUniquenessStore.BuildSql("hangfire");

        [Test]
        public void EverythingIsClaimedBySingleStatement()
        {
            _sql.TrimEnd().Split(';', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(1);
        }

        [Test]
        public void ClaimsAreWrittenToTheSetTableOfTheConfiguredSchema()
        {
            _sql.Should().Contain(@"INSERT INTO ""hangfire"".""set""");
            PostgresJobUniquenessStore.BuildSql("other").Should().NotContain(@"""hangfire""");
        }

        [Test]
        public void TheUniqueIndexArbitratesAndExpiredClaimsAreTakenOver()
        {
            _sql.Should().Contain("""ON CONFLICT ("key", "value") DO UPDATE""")
                .And.Contain(@"""set"".""score"" <= @now")
                .And.Contain(@"RETURNING ""key"", ""value""");
        }

        [Test]
        public void TheScoreDoublesAsTheRowExpiration()
        {
            _sql.Should().Contain(@"to_timestamp(t.""score"")");
        }
    }
}
