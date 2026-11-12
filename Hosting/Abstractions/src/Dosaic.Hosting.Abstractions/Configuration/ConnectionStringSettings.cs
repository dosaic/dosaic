using System.Data.Common;

namespace Dosaic.Hosting.Abstractions.Configuration
{
    /// <summary>
    ///     Builds a connection string from a raw <c>connectionParameters</c> value and discrete configuration
    ///     keys with the same rules for every plugin:
    ///     <list type="number">
    ///         <item>the raw connection parameters are applied first,</item>
    ///         <item>a discrete key that is configured overrides them,</item>
    ///         <item>a default of the plugin only fills in a parameter that neither of them sets.</item>
    ///     </list>
    /// </summary>
    public sealed class ConnectionStringSettings<TBuilder> where TBuilder : DbConnectionStringBuilder, new()
    {
        private readonly HashSet<string> _parameterKeys;

        internal ConnectionStringSettings(TBuilder builder)
        {
            Builder = builder;
            _parameterKeys = new HashSet<string>(builder.Keys.Cast<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public TBuilder Builder { get; }

        /// <summary>Applies <paramref name="set" /> when the discrete key is configured.</summary>
        public ConnectionStringSettings<TBuilder> Override(bool configured, Action<TBuilder> set)
        {
            if (configured) set(Builder);
            return this;
        }

        /// <summary>
        ///     Applies <paramref name="set" /> when the discrete key is configured, or as a default when the raw
        ///     connection parameters do not set that parameter.
        /// </summary>
        public ConnectionStringSettings<TBuilder> OverrideOrDefault(bool configured, Action<TBuilder> set)
        {
            if (configured || !IsSetByParameters(set)) set(Builder);
            return this;
        }

        /// <summary>
        ///     Keywords have aliases (<c>Maximum Pool Size</c>, <c>MaxPoolSize</c>), so the setter is run against
        ///     an empty builder to learn the keyword the builder itself uses.
        /// </summary>
        private bool IsSetByParameters(Action<TBuilder> set)
        {
            var probe = new TBuilder();
            set(probe);
            return probe.Keys.Cast<string>().Any(_parameterKeys.Contains);
        }
    }

    public static class ConnectionStringSettings
    {
        /// <param name="configurationKey">The full key of the raw value, used in the error message.</param>
        /// <param name="connectionParameters">The raw connection string, null or empty for none.</param>
        /// <exception cref="InvalidOperationException">
        ///     The value is not a valid connection string. The message repeats neither the value nor the
        ///     message of the parser, because both may carry credentials.
        /// </exception>
        public static ConnectionStringSettings<TBuilder> Parse<TBuilder>(string configurationKey,
            string connectionParameters) where TBuilder : DbConnectionStringBuilder, new()
        {
            var builder = new TBuilder();
            try
            {
                builder.ConnectionString = connectionParameters ?? "";
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"Configuration: {configurationKey} is not a valid connection string ({e.GetType().Name}).");
            }

            return new ConnectionStringSettings<TBuilder>(builder);
        }

        /// <summary>
        ///     Parses an enum by name, ignoring case. A hyphen can separate the words of a name, so the libpq
        ///     spelling <c>verify-full</c> parses as <c>VerifyFull</c>. Numbers are refused although
        ///     <c>Enum.TryParse</c> accepts them, because they would silently pick a value nobody wrote down.
        /// </summary>
        public static TEnum ParseEnumName<TEnum>(string configurationKey, string value) where TEnum : struct, Enum
        {
            var words = value.Trim().Split('-');
            if (words.All(x => x.Length > 0 && x.All(char.IsLetter))
                && Enum.TryParse<TEnum>(string.Concat(words), true, out var result))
                return result;
            throw new InvalidOperationException(
                $"Configuration: {configurationKey} '{value}' is not a valid {typeof(TEnum).Name}. " +
                $"Expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}. " +
                "Case is ignored and a hyphen can separate the words.");
        }
    }
}
