using System.Collections.Immutable;
using MimeDetective.Definitions;
using MimeDetective.Storage;

namespace Dosaic.Plugins.Persistence.S3.File;

[Flags]
public enum FileType
{
    Any = 0,
    All = 1 << 0,
    Archives = 1 << 1,
    Documents = 1 << 2,
    Email = 1 << 3,
    Images = 1 << 4,
    Text = 1 << 5,
    Xml = 1 << 6,
}

public interface IFileTypeDefinitionResolver
{
    ImmutableArray<Definition> GetDefinitions(FileType fileType);
}

public class DefaultFileTypeDefinitionResolver : IFileTypeDefinitionResolver
{
    public ImmutableArray<Definition> GetDefinitions(FileType fileType)
    {
        return fileType switch
        {
            FileType.Any => [],
            FileType.Archives => DefaultDefinitions.FileTypes.Archives.All(),
            FileType.Documents => DefaultDefinitions.FileTypes.Documents.All(),
            FileType.Email => DefaultDefinitions.FileTypes.Email.All(),
            FileType.Images => DefaultDefinitions.FileTypes.Images.All(),
            FileType.Text => DefaultDefinitions.FileTypes.Text.All(),
            FileType.Xml => DefaultDefinitions.FileTypes.Xml.All(),
            _ => DefaultDefinitions.All()
        };
    }
}
