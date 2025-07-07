using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Extensions.Abstractions.Tests
{
    public class QuantityTests
    {

        [Test]
        public void CustomQuantityInitConstructorShouldWorkCorrectly()
        {
            var customQuantity = new CustomQuantity { Value = "myValue", Unit = "myUnit" };
            customQuantity.Unit.Should().Be("myUnit");
            customQuantity.Value.Should().Be("myValue");
        }

        [Test]
        public void CountQuantityShouldEnforceUnitCount()
        {
            var loginCount = new LoginQuantityCount(10);
            loginCount.Unit.Should().Be("Count");
            loginCount.Value.Should().Be(10);
        }

        [Test]
        public void CountQuantityShouldSerializeCorrectly()
        {
            var myCounts = new MyCounts() { LoginCount = new LoginQuantityCount(10) };
            myCounts.LoginCount.Value.Should().Be(10);
            var json = JsonSerializer.Serialize(myCounts, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            json.Should().Be("{\"loginCount\":{\"value\":10,\"unit\":\"Count\"}}");
        }

        [Test]
        public void QuantityTypesShouldBeDistinctWhenValuesAreSame()
        {
            var loginCount = new LoginQuantityCount(5);
            var otherLoginCount = new LoginQuantityCount(5);
            loginCount.Should().Be(otherLoginCount);

            var customQuantity = new CustomQuantity { Value = "5", Unit = "Count" };
            customQuantity.Should().NotBe(loginCount);
        }

        [Test]
        public void QuantityEqualityShouldConsiderValueAndType()
        {
            var loginCount1 = new LoginQuantityCount(10);
            var loginCount2 = new LoginQuantityCount(10);
            var loginCount3 = new LoginQuantityCount(20);

            loginCount1.Should().Be(loginCount2);
            loginCount1.Should().NotBe(loginCount3);
        }

        [Test]
        public void DeserializingQuantityShouldRestoreValues()
        {
            var original = new MyCounts { LoginCount = new LoginQuantityCount(42) };
            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var deserialized = JsonSerializer.Deserialize<MyCounts>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            deserialized.Should().NotBeNull();
            deserialized!.LoginCount.Value.Should().Be(42);
            deserialized.LoginCount.Unit.Should().Be("Count");
        }

        [Test]
        public void ZeroValueQuantityShouldBeValid()
        {
            var zeroCount = new LoginQuantityCount(0);
            zeroCount.Value.Should().Be(0);
            zeroCount.Unit.Should().Be("Count");
        }

        [Test]
        public void NegativeValueQuantityShouldPreserveSign()
        {
            var negativeCount = new LoginQuantityCount(-5);
            negativeCount.Value.Should().Be(-5);
            negativeCount.Unit.Should().Be("Count");
        }

        private class MyCounts
        {
            public LoginQuantityCount LoginCount { get; set; } = null!;
        }

        private record LoginQuantityCount(int Value) : QuantityCount(Value);

        private record CustomQuantity() : Quantity<string>("test", "test");
    }

    namespace Dosaic.Extensions.Abstractions.Tests
    {
    }
}
