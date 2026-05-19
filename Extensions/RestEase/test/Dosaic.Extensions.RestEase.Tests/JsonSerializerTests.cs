using System.Net.Http;
using System.Text.Json;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Json;
using NUnit.Framework;
using RestEase;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class JsonSerializerTests
    {
        [Test]
        public void SerializerReturnsNullForNullBody()
        {
            var sut = new SystemTextJsonRequestBodySerializer(new JsonSerializerOptions());
            sut.SerializeBody<SomeResource>(null, default(RequestBodySerializerInfo)).Should().BeNull();
        }

        [Test]
        public void SerializerProducesJsonStringContent()
        {
            var sut = new SystemTextJsonRequestBodySerializer(new JsonSerializerOptions());
            var content = sut.SerializeBody(new SomeResource { Name = "x" }, default(RequestBodySerializerInfo));
            content.Should().NotBeNull();
            content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        }

        [Test]
        public void DeserializerReturnsDefaultForEmptyContent()
        {
            var sut = new SystemTextJsonResponseDeserializer(new JsonSerializerOptions());
            var result = sut.Deserialize<SomeResource>("", new HttpResponseMessage(), default(ResponseDeserializerInfo));
            result.Should().BeNull();
        }

        [Test]
        public void DeserializerProducesValue()
        {
            var sut = new SystemTextJsonResponseDeserializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var result = sut.Deserialize<SomeResource>("{\"name\":\"x\"}", new HttpResponseMessage(), default(ResponseDeserializerInfo));
            result!.Name.Should().Be("x");
        }
    }
}
