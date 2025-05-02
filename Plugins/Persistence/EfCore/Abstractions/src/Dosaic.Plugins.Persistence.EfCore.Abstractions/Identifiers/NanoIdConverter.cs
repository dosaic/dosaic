using System.Diagnostics.CodeAnalysis;
using Dosaic.Extensions.NanoIds;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers
{
    [ExcludeFromCodeCoverage(Justification = "Value converter is tested by ef core")]
    public class NanoIdConverter() : ValueConverter<NanoId, string>(x => x.Value, x => new NanoId(x));

    public class DbNanoIdPrimaryKeyAttribute(byte length, string prefix = "") : NanoIdAttribute(length, prefix);
}
