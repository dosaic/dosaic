namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    /// <summary>
    /// Provides user information for auditing purposes
    /// </summary>
    public interface IUserIdProvider
    {
        /// <summary>
        /// Gets the current authenticated user
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Gets the fallback user id
        /// </summary>
        string FallbackUserId { get; }

        /// <summary>
        /// Checks if it is a user interaction
        /// </summary>
        bool IsUserInteraction { get; }
    }
}
