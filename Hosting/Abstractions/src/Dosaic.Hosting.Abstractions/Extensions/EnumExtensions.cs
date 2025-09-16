using System.Globalization;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class EnumExtensions
    {
        public static bool IsFlagSet<T>(this T value, T flag) where T : Enum
        {
            var intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            var flagValue = Convert.ToInt32(flag, CultureInfo.InvariantCulture);
            return (intValue & flagValue) == flagValue;
        }
    }
}
