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
    }
}
