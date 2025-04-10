using FluentAssertions;
using NUnit.Framework;
using Sqids;

namespace Dosaic.Extensions.Sqids.Tests
{
    [TestFixture]
    public class SqidExtensionsTests
    {
        private SqidsEncoder<char> _customEncoder;
        private SqidsOptions _options;


        [SetUp]
        public void Setup()
        {
            _options = new SqidsOptions { Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789", MinLength = 5 };
            _customEncoder = new SqidsEncoder<char>(_options);
        }

        [Test]
        public void ToSqidWithDefaultEncoder()
        {
            var input = "Hello";
            var sqid = input.ToSqid();

            sqid.Should().NotBeNullOrEmpty();
            sqid.Should().NotBe(input);
        }

        [Test]
        public void ToSqidWithCustomEncoder()
        {
            var input = "Hello";
            var sqid = input.ToSqid(_customEncoder);

            sqid.Should().NotBeNullOrEmpty();
            sqid.Should().NotBe(input);
        }

        [Test]
        public void FromSqidWithDefaultEncoder()
        {
            var original = "Test";
            var sqid = original.ToSqid();

            var result = sqid.FromSqid();

            result.Should().Be(original);
        }

        [Test]
        public void FromSqidWithModifiedDefaultEncoder()
        {
            SqidExtensions.Encoder =
                new SqidsEncoder<char>(new SqidsOptions { Alphabet = "asdfjkl", MinLength = 2 });
            var original = "Test";
            var sqid = original.ToSqid();

            var result = sqid.FromSqid();

            result.Should().Be(original);
        }

        [Test]
        public void FromSqidWithCustomEncoder()
        {
            var original = "Test";
            var sqid = original.ToSqid(_customEncoder);

            var result = sqid.FromSqid(_customEncoder);

            result.Should().Be(original);
        }

        [Test]
        public void RoundTripWithDefaultEncoder()
        {
            var original = "ComplexString123";
            var sqid = original.ToSqid();
            var result = sqid.FromSqid();

            result.Should().Be(original);
        }

        [Test]
        public void EmptyStringToSqid()
        {
            var input = "";
            var sqid = input.ToSqid();

            sqid.Should().NotBeNull();
        }

        [Test]
        public void EmptyStringFromSqid()
        {
            var emptyEncoded = "".ToSqid();
            var result = emptyEncoded.FromSqid();

            result.Should().BeEmpty();
        }

        [Test]
        public void MinLengthIsRespected()
        {
            var shortInput = "A";
            var sqid = shortInput.ToSqid();

            sqid.Length.Should().BeGreaterThanOrEqualTo(_options.MinLength);
        }

        [Test]
        public void NullStringToSqidThrowsArgumentNullException()
        {
            string input = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            Action act = () => input.ToSqid();

            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void NullStringFromSqidThrowsArgumentNullException()
        {
            string sqid = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            Action act = () => sqid.FromSqid();

            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void SpecialCharactersToSqid()
        {
            var input = "!@#$%^&*()";
            var sqid = input.ToSqid();

            sqid.Should().NotBeNullOrEmpty();
            sqid.Should().NotBe(input);
        }

        [Test]
        public void LongStringToSqidAndBack()
        {
            var input = new string('A', 1000);
            var sqid = input.ToSqid();
            var result = sqid.FromSqid();

            result.Should().Be(input);
        }

        [Test]
        public void NonAsciiCharactersToSqid()
        {
            var input = "你好世界";
            var sqid = input.ToSqid();

            sqid.Should().NotBeNullOrEmpty();
            sqid.Should().NotBe(input);
        }

        [Test]
        public void NonAsciiCharactersFromSqid()
        {
            var original = "你好世界";
            var sqid = original.ToSqid();
            var result = sqid.FromSqid();

            result.Should().Be(original);
        }
    }
}
