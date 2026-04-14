namespace Dosaic.Plugins.Jobs.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class JobTimeoutAttribute : Attribute
    {
        public JobTimeoutAttribute(int timeout, TimeUnit unit)
        {
            Timeout = unit switch
            {
                TimeUnit.Milliseconds => TimeSpan.FromMilliseconds(timeout),
                TimeUnit.Seconds => TimeSpan.FromSeconds(timeout),
                TimeUnit.Minutes => TimeSpan.FromMinutes(timeout),
                TimeUnit.Hours => TimeSpan.FromHours(timeout),
                TimeUnit.Days => TimeSpan.FromDays(timeout),
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
            };
        }

        public TimeSpan Timeout { get; }
    }

    public enum TimeUnit
    {
        Milliseconds,
        Seconds,
        Minutes,
        Hours,
        Days
    }
}
