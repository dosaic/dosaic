using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dosaic.Plugins.Jobs.Hangfire.Uniqueness
{
    /// <summary>
    ///     Stable identity of "the same job, with the same arguments, on the same queue".
    ///     Uniqueness is enforced by claiming that identity in the storage and letting the unique index
    ///     decide the winner, instead of scanning the queue for an equivalent job.
    /// </summary>
    internal static class JobFingerprint
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { IncludeFields = false };
        private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        ///     Job parameter holding the fingerprint a job currently owns. Only the owner is allowed to
        ///     release the claim, and only the owner may pass a state election that it would otherwise lose
        ///     against its own claim.
        /// </summary>
        public const string ClaimParameterName = "DosaicUniqueClaim";

        /// <summary>Hangfire set all claims of one queue live in. Must stay below the 100 character key limit.</summary>
        public static string SetKey(string queue) => $"dosaic:unique:{queue}";

        public static string Compute(global::Hangfire.Common.Job job, string queue)
        {
            var arguments = job.Args.Where(x => x is not CancellationToken).ToList();
            // deliberately built from the method name and its parameter types instead of the MethodInfo:
            // an expression over the job interface resolves the interface method, one over the concrete
            // class the implementation, and both have to produce the same fingerprint
            var payload = new StringBuilder()
                .Append(queue).Append('|')
                .Append(job.Type.FullName).Append('|')
                .Append(job.Method.Name).Append('|')
                .Append(string.Join(",", job.Method.GetParameters().Select(x => x.ParameterType.FullName)))
                .Append('|')
                .Append(JsonSerializer.Serialize(arguments, _jsonSerializerOptions))
                .ToString();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }

        public static double ToTimestamp(DateTime value) => (value.ToUniversalTime() - _epoch).TotalSeconds;
    }
}
