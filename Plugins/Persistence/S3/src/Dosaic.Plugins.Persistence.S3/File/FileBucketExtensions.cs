using System.Reflection;

namespace Dosaic.Plugins.Persistence.S3.File;

public static class FileBucketExtensions
{
    private static FileBucketAttribute GetBucketAttribute<T>(T bucket) where T : Enum
    {
        return typeof(T).GetMember(bucket.ToString())[0].GetCustomAttribute<FileBucketAttribute>()!;
    }

    public static string GetName<T>(this T bucket) where T : Enum => GetBucketAttribute(bucket).Name;
    public static FileType GetFileType<T>(this T bucket) where T : Enum => GetBucketAttribute(bucket).FileType;

}
