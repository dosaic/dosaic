using System.Reflection;

namespace Dosaic.Plugins.Persistence.S3.File;

public static class FileBucketExtensions
{
    private static FileBucketAttribute GetBucketAttribute<T>(T bucket) where T : Enum
    {
        return typeof(T).GetMember(bucket.ToString())[0].GetCustomAttribute<FileBucketAttribute>()!;
    }

    public static T GetEnumValueFromAttributeName<T>(string bucket) where T : Enum
    {
        var enumValue = typeof(T).GetMembers()
            .FirstOrDefault(x => x.GetCustomAttribute<FileBucketAttribute>()?.Name == bucket);
        Enum.TryParse(typeof(T), enumValue?.Name, true, out var result);
        return result != null
            ? (T)result
            : throw new ArgumentException($"No enum value found for bucket name '{bucket}' in {typeof(T).Name}.");
    }

    public static string GetName<T>(this T bucket) where T : Enum => GetBucketAttribute(bucket).Name;
    public static FileType GetFileType<T>(this T bucket) where T : Enum => GetBucketAttribute(bucket).FileType;

}
