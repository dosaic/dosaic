using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.DependencyInjection;
using Unleash;

namespace Dosaic.Plugins.Management.Unleash
{
    [Middleware(50)]
    public class UnleashMiddlware : ApiMiddleware
    {
        private readonly bool _sessionFeatureEnabled;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly UnleashConfiguration _unleashConfiguration;
        internal const string Unleashcontext = "unleashContext";

        public UnleashMiddlware(RequestDelegate next, IDateTimeProvider dateTimeProvider,
            UnleashConfiguration unleashConfiguration, IServiceProvider serviceProvider) : base(next, dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;
            _unleashConfiguration = unleashConfiguration;
            _sessionFeatureEnabled = serviceProvider.GetService<ISessionStore>() != null;
        }

        public override async Task Invoke(HttpContext context)
        {
            var unleashContext = new UnleashContext
            {
                UserId = context.User?.Identity?.Name,
                AppName = _unleashConfiguration.AppName,
                CurrentTime = _dateTimeProvider.UtcNow,
                RemoteAddress = context.Request?.Host.Host,
                Properties = new Dictionary<string, string>()
            };
            if (_sessionFeatureEnabled)
            {
                unleashContext.SessionId = context.Session?.Id;
            }

            context.Items[Unleashcontext] = unleashContext;
            await Next.Invoke(context);
        }
    }
}
