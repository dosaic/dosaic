using Sqids;

namespace Dosaic.Extensions.Sqids;

public static class SqidExtensions
{
    public static SqidsEncoder<char> Encoder { get; set; } = new(new SqidsOptions
    {
        Alphabet = "kKsW7PVdXUYnHgQ6rujl0GepfNzB2qZ9bC83IyDmOAtJ4hcSvM1Roaw5LxEiTF",
        MinLength = 10
    });

    public static string ToSqid(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return Encoder.Encode(str.ToCharArray());
    }

    public static string ToSqid(this string str, SqidsEncoder<char> encoder)
    {
        ArgumentNullException.ThrowIfNull(str);
        return encoder.Encode(str.ToCharArray());
    }

    public static string FromSqid(this string sqid)
    {
        ArgumentNullException.ThrowIfNull(sqid);
        return string.Concat(Encoder.Decode(sqid));
    }

    public static string FromSqid(this string sqid, SqidsEncoder<char> encoder)
    {
        ArgumentNullException.ThrowIfNull(sqid);
        return string.Concat(encoder.Decode(sqid));
    }
}
