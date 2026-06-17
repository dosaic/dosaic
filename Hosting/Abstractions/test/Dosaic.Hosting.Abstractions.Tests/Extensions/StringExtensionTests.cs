using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Extensions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class StringExtensionTests
    {
        [Test]
        [TestCase("TestString", "test_string")]
        [TestCase("testString", "test_string")]
        [TestCase("Test", "test")]
        [TestCase("test", "test")]
        [TestCase("TestStringExample", "test_string_example")]
        public void ToSnakeCaseWorks(string input, string expected)
        {
            var result = input.ToSnakeCase();
            result.Should().Be(expected);
        }

        [TestCase("", "")]
        [TestCase("HelloWorld123_-~.", "HelloWorld123_-~.")]
        [TestCase("a b", "a%20b")]
        [TestCase("a&b=c?d e", "a%26b%3Dc%3Fd%20e")]
        [TestCase("äöü ß&€", "%C3%A4%C3%B6%C3%BC%20%C3%9F%26%E2%82%AC")]
        public void ToUrlEncodedUtf8_Encodes_AsExpected(string input, string expected)
        {
            var encoded = input.ToUrlEncoded();
            encoded.Should().Be(expected);
        }

        [TestCase("", "")]
        [TestCase("%C3%A4%C3%B6%C3%BC%20%C3%9F%26%E2%82%AC", "äöü ß&€")]
        [TestCase("a%26b%3Dc%3Fd%20e", "a&b=c?d e")]
        [TestCase("plain-text_123", "plain-text_123")]
        public void FromUrlEncodedUtf8_Decodes_AsExpected(string input, string expected)
        {
            var decoded = input.FromUrlEncoded();
            decoded.Should().Be(expected);
        }

        private static readonly string[] RoundTripSamples =
        {
            "simple",
            "a b c",
            "äöü ß&€",
            "param=wert&x=y z",
            "symbols: !@#$%^*()[]{}|\\;:'\",<.>~`"
        };

        [TestCaseSource(nameof(RoundTripSamples))]
        public void UrlEncodingDecode_IsInverse_OfEncode_ForTypicalInputs(string sample)
        {
            var encoded = sample.ToUrlEncoded();
            var roundTrip = encoded.FromUrlEncoded();

            roundTrip.Should().Be(sample, "round-trip should preserve '{0}'", sample);
        }

        private enum SampleEnum
        {
            First,
            Second,
            Third
        }

        [Test]
        public void ParseEnumReturnsValueWhenStringMatches()
        {
            "Second".ParseEnum<SampleEnum>().Should().Be(SampleEnum.Second);
        }

        [Test]
        public void ParseEnumIsCaseInsensitive()
        {
            "second".ParseEnum<SampleEnum>().Should().Be(SampleEnum.Second);
            "THIRD".ParseEnum<SampleEnum>().Should().Be(SampleEnum.Third);
        }

        [Test]
        public void ParseEnumReturnsNullWhenStringDoesNotMatch()
        {
            "Unknown".ParseEnum<SampleEnum>().Should().BeNull();
        }

        [Test]
        public void ParseEnumReturnsNullForNullInput()
        {
            ((string?)null).ParseEnum<SampleEnum>().Should().BeNull();
        }

        [Test]
        public void ParseEnumReturnsNullForEmptyInput()
        {
            string.Empty.ParseEnum<SampleEnum>().Should().BeNull();
        }

        [Test]
        public void ParseEnumReturnsNullForUndefinedNumericValue()
        {
            "999".ParseEnum<SampleEnum>().Should().BeNull();
        }

        [Test]
        public void TryParseEnumReturnsTrueAndValueWhenMatches()
        {
            var result = "First".TryParseEnum(typeof(SampleEnum), out var value);

            result.Should().BeTrue();
            value.Should().Be(SampleEnum.First);
        }

        [Test]
        public void TryParseEnumIsCaseInsensitive()
        {
            var result = "third".TryParseEnum(typeof(SampleEnum), out var value);

            result.Should().BeTrue();
            value.Should().Be(SampleEnum.Third);
        }

        [Test]
        public void TryParseEnumReturnsFalseWhenStringDoesNotMatch()
        {
            var result = "Unknown".TryParseEnum(typeof(SampleEnum), out var value);

            result.Should().BeFalse();
            value.Should().BeNull();
        }

        [Test]
        public void TryParseEnumReturnsFalseForNullInput()
        {
            var result = ((string?)null).TryParseEnum(typeof(SampleEnum), out var value);

            result.Should().BeFalse();
            value.Should().BeNull();
        }

        [Test]
        public void TryParseEnumReturnsFalseForUndefinedNumericValue()
        {
            var result = "999".TryParseEnum(typeof(SampleEnum), out var value);

            result.Should().BeFalse();
            value.Should().BeNull();
        }

        [Test]
        public void NormalizeNullAndEmptyValuesReturnsNullForNull()
        {
            ((string?)null).NormalizeNullAndEmptyValues().Should().BeNull();
        }

        [Test]
        public void NormalizeNullAndEmptyValuesReturnsNullForEmpty()
        {
            string.Empty.NormalizeNullAndEmptyValues().Should().BeNull();
        }

        [Test]
        public void NormalizeNullAndEmptyValuesReturnsNullForUnbekannt()
        {
            "unbekannt".NormalizeNullAndEmptyValues().Should().BeNull();
            "Unbekannt".NormalizeNullAndEmptyValues().Should().BeNull();
            "UNBEKANNT".NormalizeNullAndEmptyValues().Should().BeNull();
        }

        [Test]
        public void NormalizeNullAndEmptyValuesReturnsNullForNullString()
        {
            "null".NormalizeNullAndEmptyValues().Should().BeNull();
            "Null".NormalizeNullAndEmptyValues().Should().BeNull();
            "NULL".NormalizeNullAndEmptyValues().Should().BeNull();
        }

        [Test]
        public void NormalizeNullAndEmptyValuesReturnsValueWhenMeaningful()
        {
            "Hello".NormalizeNullAndEmptyValues().Should().Be("Hello");
        }

        [Test]
        public void NormalizeNullAndEmptyValuesPreservesWhitespace()
        {
            " ".NormalizeNullAndEmptyValues().Should().Be(" ");
        }

        [Test]
        public void TruncateReturnsOriginalStringWhenShorterThanMaxLength()
        {
            "Hello".Truncate(10).Should().Be("Hello");
        }

        [Test]
        public void TruncateReturnsTruncatedStringWhenLongerThanMaxLength()
        {
            "Hello, World!".Truncate(5).Should().Be("Hello");
        }
    }
}
