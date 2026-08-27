namespace Dosaic.Plugins.Jobs.Hangfire.Uniqueness
{
    /// <summary>
    ///     A single "this fingerprint is taken" marker.
    /// </summary>
    /// <param name="SetKey">Hangfire set the marker is stored in.</param>
    /// <param name="Fingerprint">Value that must be unique inside <paramref name="SetKey" />.</param>
    /// <param name="ExpiresAt">
    ///     Unix seconds after which the claim may be taken over by somebody else. Safety net for claims
    ///     that were never released because the owning process died.
    /// </param>
    internal sealed record JobUniquenessClaim(string SetKey, string Fingerprint, double ExpiresAt);

    /// <summary>
    ///     Takes ownership of job fingerprints. Replaces scanning the queue for an equivalent job: the
    ///     storage decides the winner, so the check is O(1) and free of the read/write race the scan had.
    /// </summary>
    internal interface IJobUniquenessStore
    {
        /// <summary>
        ///     Tries to take every claim in a single round trip and returns the subset that is now owned by
        ///     the caller. Everything not returned is currently owned by another job.
        /// </summary>
        /// <param name="claims">Claims to take. Must not contain the same set key/fingerprint pair twice.</param>
        /// <param name="now">Current time as unix seconds, used to detect expired claims.</param>
        IReadOnlyCollection<JobUniquenessClaim> Claim(IReadOnlyList<JobUniquenessClaim> claims, double now);
    }
}
