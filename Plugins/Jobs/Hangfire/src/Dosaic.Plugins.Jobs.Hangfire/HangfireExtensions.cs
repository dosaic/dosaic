using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    public static class HangfireExtensions
    {
        public static void ConfigureJobs(this IServiceCollection serviceCollection, Action<IJobManager, IServiceProvider> configure)
        {
            serviceCollection.AddSingleton(new JobOptions(configure));
        }
    }
}
