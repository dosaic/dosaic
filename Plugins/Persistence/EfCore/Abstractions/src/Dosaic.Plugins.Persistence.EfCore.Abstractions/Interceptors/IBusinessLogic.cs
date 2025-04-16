using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors
{
    public interface IBusinessLogic<in TModel> where TModel : class, IModel
    {
        Task BeforeCreateAsync(TModel model, CancellationToken cancellationToken) => Task.CompletedTask;
        Task BeforeUpdateAsync(TModel model, CancellationToken cancellationToken) => Task.CompletedTask;
        Task BeforeDeleteAsync(TModel model, CancellationToken cancellationToken) => Task.CompletedTask;

        Task BeforeAsync(ChangeState state, TModel model, CancellationToken cancellationToken) => state switch
        {
            ChangeState.Added => BeforeCreateAsync(model, cancellationToken),
            ChangeState.Modified => BeforeUpdateAsync(model, cancellationToken),
            ChangeState.Deleted => BeforeDeleteAsync(model, cancellationToken),
            _ => Task.CompletedTask
        };

        Task AfterCreateAsync(TModel model, CancellationToken cancellationToken) => Task.CompletedTask;
        Task AfterUpdateAsync(TModel model, CancellationToken cancellationToken) => Task.CompletedTask;
        Task AfterDeleteAsync(TModel model, CancellationToken cancellationToken) => Task.CompletedTask;

        Task AfterAsync(ChangeState state, TModel model, CancellationToken cancellationToken) => state switch
        {
            ChangeState.Added => AfterCreateAsync(model, cancellationToken),
            ChangeState.Modified => AfterUpdateAsync(model, cancellationToken),
            ChangeState.Deleted => AfterDeleteAsync(model, cancellationToken),
            _ => Task.CompletedTask
        };
    }
}
