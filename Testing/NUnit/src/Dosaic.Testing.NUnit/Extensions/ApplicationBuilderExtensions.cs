using Microsoft.AspNetCore.Builder;
using NSubstitute;
using NSubstitute.Core;

namespace Dosaic.Testing.NUnit.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static List<ICall> GetReceivedMiddlewareCalls(this IApplicationBuilder appBuilder)
        {
            var useCalls = (
                from call in appBuilder.ReceivedCalls()
                where call.GetMethodInfo().Name == "Use"
                select call).ToList();
            return useCalls;
        }
    }
}
