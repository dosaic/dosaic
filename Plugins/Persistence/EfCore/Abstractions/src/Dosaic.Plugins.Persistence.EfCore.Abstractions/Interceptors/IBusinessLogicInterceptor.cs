using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors
{
    internal interface IBusinessLogicInterceptor
    {
        Task InterceptBeforeAsync(IModel model, ChangeState entityState, CancellationToken cancellationToken);
        Task InterceptAfterAsync(IModel model, ChangeState entityState, CancellationToken cancellationToken);
    }

}
