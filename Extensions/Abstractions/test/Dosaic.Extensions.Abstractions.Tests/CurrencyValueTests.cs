using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Extensions.Abstractions.Tests
{
    public class CurrencyValueTests
    {
        [Test]
        public void CurrencyValueEuroWorksAsExpected()
        {
            var euro = CurrencyValue.Euro(100.99m);
            euro.Value.Should().Be(100.99m);
            euro.CurrencySymbol.Should().Be("€");
            euro.CurrencyCode.Should().Be("EUR");
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(euro, jsonSerializerOptions);
            json.Should().Be("{\"value\":100.99,\"currencySymbol\":\"\\u20AC\",\"currencyCode\":\"EUR\"}");
            var deserialize = JsonSerializer.Deserialize<CurrencyValue>(json, jsonSerializerOptions);
            deserialize!.Value.Should().Be(100.99m);
            deserialize.CurrencySymbol.Should().Be("€");
            deserialize.CurrencyCode.Should().Be("EUR");
        }

        [Test]
        public void CurrencyValueDollarWorksAsExpected()
        {
            var euro = CurrencyValue.Dollar(10.99m);
            euro.Value.Should().Be(10.99m);
            euro.CurrencySymbol.Should().Be("$");
            euro.CurrencyCode.Should().Be("USD");
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(euro, jsonSerializerOptions);
            json.Should().Be("{\"value\":10.99,\"currencySymbol\":\"$\",\"currencyCode\":\"USD\"}");
            var deserialize = JsonSerializer.Deserialize<CurrencyValue>(json, jsonSerializerOptions);
            deserialize!.Value.Should().Be(10.99m);
            deserialize.CurrencySymbol.Should().Be("$");
            deserialize.CurrencyCode.Should().Be("USD");
        }

        [Test]
        public void CurrencyValueShouldSerializeCorrectly()
        {
            var euro = new CurrencyValue(999.99m, "£", "GBP");
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(euro, jsonSerializerOptions);
            json.Should().Be("{\"value\":999.99,\"currencySymbol\":\"\\u00A3\",\"currencyCode\":\"GBP\"}");
            var deserialize = JsonSerializer.Deserialize<CurrencyValue>(json, jsonSerializerOptions);
            deserialize!.Value.Should().Be(999.99m);
            deserialize.CurrencySymbol.Should().Be("£");
            deserialize.CurrencyCode.Should().Be("GBP");
        }
    }
}
