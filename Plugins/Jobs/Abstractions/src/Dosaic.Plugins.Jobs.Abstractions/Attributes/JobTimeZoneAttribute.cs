namespace Dosaic.Plugins.Jobs.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class JobTimeZoneAttribute : Attribute
    {
        public JobTimeZoneAttribute(string timeZoneId)
        {
            TimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        public JobTimeZoneAttribute(TimeZoneInfo timeZoneInfo)
        {
            TimeZoneInfo = timeZoneInfo;
        }

        public TimeZoneInfo TimeZoneInfo { get; }
    }
}
