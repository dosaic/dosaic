using TickerQ.Utilities.Enums;

namespace Dosaic.Plugins.Jobs.TickerQ.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class JobPriorityAttribute : Attribute
    {
        public JobPriorityAttribute(TickerTaskPriority priority)
        {
            Priority = priority;
        }

        public TickerTaskPriority Priority { get; }
    }
}
