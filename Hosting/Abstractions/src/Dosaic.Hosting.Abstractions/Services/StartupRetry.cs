using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dosaic.Hosting.Abstractions.Services
{
    /// <summary>
    ///     Retry settings of a task that runs once when the host starts, such as a database or bucket migration.
    /// </summary>
    public class StartupRetryOptions
    {
        /// <summary>
        ///     Number of attempts, the first one included. 0 or less retries until the host stops.
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        ///     True stops the host with exit code 1 when the last attempt failed. False (default) logs the error
        ///     and keeps the host running.
        /// </summary>
        public bool StopHostOnFailure { get; set; }
    }

    public static class StartupRetry
    {
        /// <summary>
        ///     Runs <paramref name="action" /> until it succeeds, <see cref="StartupRetryOptions.MaxAttempts" /> is
        ///     reached or <paramref name="cancellationToken" /> is cancelled. Every failure is logged with the
        ///     attempt number. After the last failed attempt the host is stopped with exit code 1 when
        ///     <see cref="StartupRetryOptions.StopHostOnFailure" /> is set.
        /// </summary>
        /// <param name="options">The retry settings, null retries until the host stops.</param>
        /// <param name="taskName">Completes the log line "Could not {taskName}", for example "migrate database".</param>
        /// <param name="action">The task to run.</param>
        /// <param name="retryDelay">The wait after the failed attempt with the given number (1 based).</param>
        /// <param name="logger">Receives a line per failed attempt.</param>
        /// <param name="lifetime">Stops the host on failure, may be null when the host is never stopped.</param>
        /// <param name="cancellationToken">Ends the retries without an error.</param>
        /// <returns>True when the action succeeded.</returns>
        public static async Task<bool> RunAsync(StartupRetryOptions options, string taskName,
            Func<CancellationToken, Task> action, Func<int, TimeSpan> retryDelay, ILogger logger,
            IHostApplicationLifetime lifetime, CancellationToken cancellationToken)
        {
            options ??= new StartupRetryOptions();
            var maxAttempts = options.MaxAttempts > 0 ? options.MaxAttempts : (int?)null;
            for (var attempt = 1; !cancellationToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    await action(cancellationToken);
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                catch (Exception e)
                {
                    if (maxAttempts is null || attempt < maxAttempts)
                    {
                        if (maxAttempts is null)
                            logger.LogError(e, "Could not {StartupTask} (attempt {Attempt}) -> retrying", taskName,
                                attempt);
                        else
                            logger.LogError(e, "Could not {StartupTask} (attempt {Attempt} of {MaxAttempts}) -> retrying",
                                taskName, attempt, maxAttempts);
                        try
                        {
                            await Task.Delay(retryDelay(attempt), cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            return false;
                        }

                        continue;
                    }

                    if (!options.StopHostOnFailure || lifetime is null)
                    {
                        logger.LogError(e, "Could not {StartupTask} after {Attempts} attempts -> giving up", taskName,
                            attempt);
                        return false;
                    }

                    logger.LogCritical(e, "Could not {StartupTask} after {Attempts} attempts -> stopping the host",
                        taskName, attempt);
                    Environment.ExitCode = 1;
                    lifetime.StopApplication();
                    return false;
                }
            }

            return false;
        }
    }
}
