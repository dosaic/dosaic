namespace Dosaic.Plugins.Persistence.S3.File;

[AttributeUsage(AttributeTargets.Field)]
public class FileBucketAttribute(string name, FileType fileType = FileType.All) : Attribute
{
    public string Name { get; } = name;
    public FileType FileType { get; } = fileType;
}
