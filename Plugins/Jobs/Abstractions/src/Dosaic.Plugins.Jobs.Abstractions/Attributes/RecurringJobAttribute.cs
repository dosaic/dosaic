namespace Dosaic.Plugins.Jobs.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RecurringJobAttribute : Attribute
    {
        public RecurringJobAttribute(string cronPattern, string queue = "default")
        {
            CronPattern = cronPattern;
            Queue = queue;
        }

        public string CronPattern { get; }
        public string Queue { get; }
    }
}
