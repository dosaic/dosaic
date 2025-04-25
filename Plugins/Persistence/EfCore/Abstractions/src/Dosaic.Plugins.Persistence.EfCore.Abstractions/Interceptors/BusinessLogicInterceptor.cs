using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors
{
    internal class BusinessLogicInterceptor(IServiceProvider serviceProvider) : IBusinessLogicInterceptor
    {
        public Task InterceptBeforeAsync(IModel model, ChangeState entityState, CancellationToken cancellationToken) =>
            InterceptAsync(model, entityState, nameof(IBusinessLogic<IModel>.BeforeAsync), cancellationToken);

        public async Task InterceptAfterAsync(IModel model, ChangeState entityState, CancellationToken cancellationToken) =>
            await InterceptAsync(model, entityState, nameof(IBusinessLogic<IModel>.AfterAsync), cancellationToken);

        private async Task InterceptAsync(IModel model, ChangeState entityState, string methodName,
            CancellationToken cancellationToken)
        {
            var entityType = model.GetType();
            var interceptorType = typeof(IBusinessLogic<>).MakeGenericType(entityType);
            var method = interceptorType.GetMethod(methodName)!;
            using var scope = serviceProvider.CreateScope();
            var interceptors = scope.ServiceProvider.GetServices(interceptorType);
            foreach (var interceptor in interceptors)
                await (method.Invoke(interceptor, [entityState, model, cancellationToken]) as Task)!;
        }
    }

}
