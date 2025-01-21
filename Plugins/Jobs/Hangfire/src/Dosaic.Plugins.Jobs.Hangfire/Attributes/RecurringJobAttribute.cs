using Hangfire.States;

namespace Dosaic.Plugins.Jobs.Hangfire.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RecurringJobAttribute : Attribute
    {
        public RecurringJobAttribute(string cronPattern, string queue = EnqueuedState.DefaultQueue)
        {
            CronPattern = cronPattern;
            Queue = queue;
        }

        public string CronPattern { get; }
        public string Queue { get; }
    }
}
