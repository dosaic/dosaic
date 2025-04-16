using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers
{
    internal class NanoIdConverter() : ValueConverter<NanoId, string>(x => x.Value, x => new NanoId(x));
}
