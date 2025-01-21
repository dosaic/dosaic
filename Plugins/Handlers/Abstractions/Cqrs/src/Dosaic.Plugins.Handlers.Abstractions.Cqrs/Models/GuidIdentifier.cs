using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Models
{
    public record GuidIdentifier(Guid Id) : IGuidIdentifier
    {
        public Guid Id { get; set; } = Id;
        public static GuidIdentifier Empty => new(Guid.Empty);
        public static GuidIdentifier New => new(Guid.NewGuid());
        public static GuidIdentifier Parse(string value) => new(Guid.Parse(value));
    }
}
