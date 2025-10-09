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
            var encoded = input.ToUrlEncodedUtf8();
            encoded.Should().Be(expected);
        }

        [TestCase("", "")]
        [TestCase("%C3%A4%C3%B6%C3%BC%20%C3%9F%26%E2%82%AC", "äöü ß&€")]
        [TestCase("a%26b%3Dc%3Fd%20e", "a&b=c?d e")]
        [TestCase("plain-text_123", "plain-text_123")]
        public void FromUrlEncodedUtf8_Decodes_AsExpected(string input, string expected)
        {
            var decoded = input.FromUrlEncodedUtf8();
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
            var encoded = sample.ToUrlEncodedUtf8();
            var roundTrip = encoded.FromUrlEncodedUtf8();

            roundTrip.Should().Be(sample, "round-trip should preserve '{0}'", sample);
        }
    }
}
