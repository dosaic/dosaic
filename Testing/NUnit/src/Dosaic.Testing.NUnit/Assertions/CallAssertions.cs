using AwesomeAssertions;
using Dosaic.Testing.NUnit.Extensions;
using Microsoft.AspNetCore.Http;
using NSubstitute.Core;

namespace Dosaic.Testing.NUnit.Assertions
{
    public static class CallAssertions
    {
        public static void AssertMiddleware<TMiddleware>(this ICall call)
        {
            var func = (call.GetOriginalArguments()[0] as Func<RequestDelegate, RequestDelegate>)?.Target;
            func.Should().NotBeNull();
            var registeredMiddleware = func!.GetInaccessibleValue<Type>("_middleware");
            if (registeredMiddleware.BaseType == typeof(TMiddleware))
                return;
            registeredMiddleware.Should().Be(typeof(TMiddleware));
        }
    }
}
