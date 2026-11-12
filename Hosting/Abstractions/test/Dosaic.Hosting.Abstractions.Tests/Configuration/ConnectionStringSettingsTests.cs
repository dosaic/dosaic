using System.Data.Common;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Configuration;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Configuration
{
    public class ConnectionStringSettingsTests
    {
        public enum Mode
        {
            Disable,
            Require,
            VerifyFull
        }

        private static string Build(string parameters, Action<ConnectionStringSettings<DbConnectionStringBuilder>> apply)
        {
            var settings = ConnectionStringSettings.Parse<DbConnectionStringBuilder>("section:connectionParameters",
                parameters);
            apply(settings);
            return settings.Builder.ConnectionString;
        }

        [Test]
        public void ConfiguredKeysOverrideTheParametersAndUnconfiguredOnesLeaveThem()
        {
            Build("host=raw;port=6432", s => s
                    .Override(true, b => b["host"] = "configured")
                    .Override(false, b => b["port"] = 5432))
                .Should().Be("host=configured;port=6432");
        }

        [Test]
        public void DefaultsOnlyFillInWhatTheParametersLeaveUnset()
        {
            Build("port=6432", s => s
                    .OverrideOrDefault(false, b => b["port"] = 5432)
                    .OverrideOrDefault(false, b => b["pool"] = 100))
                .Should().Be("port=6432;pool=100");
            Build("port=6432", s => s.OverrideOrDefault(true, b => b["port"] = 5432))
                .Should().Be("port=5432");
            Build(null, s => s.OverrideOrDefault(false, b => b["port"] = 5432))
                .Should().Be("port=5432");
        }

        [Test]
        public void InvalidParametersThrowWithoutRepeatingTheValue()
        {
            var act = () => ConnectionStringSettings.Parse<DbConnectionStringBuilder>("section:connectionParameters",
                "password=secret;broken");

            act.Should().Throw<InvalidOperationException>()
                .Where(x => x.Message.Contains("section:connectionParameters") && !x.Message.Contains("secret"));
        }

        [TestCase("require", Mode.Require)]
        [TestCase(" Disable ", Mode.Disable)]
        [TestCase("VerifyFull", Mode.VerifyFull)]
        [TestCase("verifyfull", Mode.VerifyFull)]
        public void EnumNamesAreParsedIgnoringCase(string value, Mode expected)
        {
            ConnectionStringSettings.ParseEnumName<Mode>("section:mode", value).Should().Be(expected);
        }

        [TestCase("verify-full")]
        [TestCase("Verify-Full")]
        [TestCase(" VERIFY-FULL ")]
        public void HyphensMaySeparateTheWordsOfAName(string value)
        {
            ConnectionStringSettings.ParseEnumName<Mode>("section:mode", value).Should().Be(Mode.VerifyFull);
        }

        [TestCase("1")]
        [TestCase("")]
        [TestCase("-")]
        [TestCase("-require")]
        [TestCase("require-")]
        [TestCase("verify--full")]
        [TestCase("verify_full")]
        [TestCase("require-full")]
        [TestCase("sometimes")]
        public void NumbersAndUnknownNamesAreRefused(string value)
        {
            var act = () => ConnectionStringSettings.ParseEnumName<Mode>("section:mode", value);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*section:mode '{value}'*Disable, Require, VerifyFull*hyphen*");
        }
    }
}
