using System.Diagnostics.CodeAnalysis;
using Dosaic.Hosting.Abstractions.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.FeatureManagement.Mvc;

namespace Dosaic.Plugins.Management.Unleash
{
    [ExcludeFromCodeCoverage(Justification = "not worth the time and resources to test this")]
    public class FeatureNotEnabledDisabledHandler : IDisabledFeaturesHandler
    {
        public Task HandleDisabledFeatures(IEnumerable<string> features, ActionExecutingContext context)
        {
            throw new NotFoundDosaicException("not found");
        }
    }
}
