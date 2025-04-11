namespace Dosaic.Plugins.Persistence.S3.File;

[Flags]
public enum FileType
{
    None = 0,
    All = 1 << 0,
    Archives = 1 << 1,
    Documents = 1 << 2,
    Email = 1 << 3,
    Images = 1 << 4,
    Text = 1 << 5,
    Xml = 1 << 6,
}
