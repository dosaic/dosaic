using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    /// <summary>
    ///     Runs the authorization filters of the dashboard in front of Hangfire and answers 404 with an empty body
    ///     when one of them refuses, so the dashboard is invisible to the caller. Hangfire itself only knows 401 and
    ///     403. The gate takes the filters out of the options, so Hangfire does not run them a second time.
    /// </summary>
    internal sealed class HangfireDashboardGate
    {
        private readonly JobStorage _storage;
        private readonly DashboardOptions _options;
        private readonly IDashboardAuthorizationFilter[] _filters;
        private readonly IDashboardAsyncAuthorizationFilter[] _asyncFilters;

        public HangfireDashboardGate(JobStorage storage, DashboardOptions options)
        {
            _storage = storage;
            _options = options;
            _filters = (options.Authorization ?? []).ToArray();
            _asyncFilters = (options.AsyncAuthorization ?? []).ToArray();
            options.Authorization = [];
            options.AsyncAuthorization = [];
        }

        public RequestDelegate Wrap(RequestDelegate next) => async httpContext =>
        {
            if (await IsAllowedAsync(httpContext))
            {
                await next(httpContext);
                return;
            }

            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            httpContext.Response.ContentLength = 0;
        };

        internal async Task<bool> IsAllowedAsync(HttpContext httpContext)
        {
            var context = new AspNetCoreDashboardContext(_storage, _options, httpContext);
            foreach (var filter in _filters)
                if (!filter.Authorize(context))
                    return false;
            foreach (var filter in _asyncFilters)
                if (!await filter.AuthorizeAsync(context))
                    return false;
            return true;
        }
    }
}
