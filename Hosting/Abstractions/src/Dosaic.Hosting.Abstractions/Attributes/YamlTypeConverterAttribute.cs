using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Dosaic.Hosting.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class YamlTypeConverterAttribute(Type converter) : Attribute
    {
        public Type Converter { get; } = converter;
    }

    public interface IYamlConverter
    {
        object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer);
        void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer);
    }
}
