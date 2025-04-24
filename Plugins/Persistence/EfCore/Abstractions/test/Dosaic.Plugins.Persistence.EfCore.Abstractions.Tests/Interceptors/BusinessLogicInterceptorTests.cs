using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
using Dosaic.Testing.NUnit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Interceptors;

public class BusinessLogicInterceptorTests
{
    private IBusinessLogicInterceptor _iBusinessLogicInterceptor;

    [SetUp]
    public void SetUp()
    {
        _iBusinessLogicInterceptor = Substitute.For<IBusinessLogicInterceptor>();
    }

    [Test]
    public async Task InterceptorHandlesNullModelGracefully()
    {
        await _iBusinessLogicInterceptor.InterceptBeforeAsync(null!, ChangeState.Added, CancellationToken.None);
        await _iBusinessLogicInterceptor.InterceptAfterAsync(null!, ChangeState.Added, CancellationToken.None);
        await _iBusinessLogicInterceptor.Received(1)
            .InterceptBeforeAsync(null!, ChangeState.Added, CancellationToken.None);
        await _iBusinessLogicInterceptor.Received(1)
            .InterceptAfterAsync(null!, ChangeState.Added, CancellationToken.None);
    }

    [Test]
    public async Task InterceptorHandlesUnknownEntityState()
    {
        var model = TestModel.GetModel();
        await _iBusinessLogicInterceptor.InterceptBeforeAsync(model, (ChangeState)999, CancellationToken.None);
        await _iBusinessLogicInterceptor.InterceptAfterAsync(model, (ChangeState)999, CancellationToken.None);
        await _iBusinessLogicInterceptor.Received(1)
            .InterceptBeforeAsync(model, (ChangeState)999, CancellationToken.None);
        await _iBusinessLogicInterceptor.Received(1)
            .InterceptAfterAsync(model, (ChangeState)999, CancellationToken.None);
    }

    [Test]
    public async Task InterceptorHandlesCancellationToken()
    {
        var model = TestModel.GetModel();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await _iBusinessLogicInterceptor.InterceptBeforeAsync(model, ChangeState.Added, cts.Token);
        await _iBusinessLogicInterceptor.InterceptAfterAsync(model, ChangeState.Added, cts.Token);
        await _iBusinessLogicInterceptor.Received(1).InterceptBeforeAsync(model, ChangeState.Added, cts.Token);
        await _iBusinessLogicInterceptor.Received(1).InterceptAfterAsync(model, ChangeState.Added, cts.Token);
    }

    [Test]
    public async Task InterceptorHandlesEmptyModel()
    {
        var model = TestModel.GetModel();
        await _iBusinessLogicInterceptor.InterceptBeforeAsync(model, ChangeState.Added, CancellationToken.None);
        await _iBusinessLogicInterceptor.InterceptAfterAsync(model, ChangeState.Added, CancellationToken.None);
        await _iBusinessLogicInterceptor.Received(1)
            .InterceptBeforeAsync(model, ChangeState.Added, CancellationToken.None);
        await _iBusinessLogicInterceptor.Received(1)
            .InterceptAfterAsync(model, ChangeState.Added, CancellationToken.None);
    }

    [Test]
    public async Task InterceptorHandlesMultipleInterceptors()
    {
        var model = TestModel.GetModel();
        var interceptor1 = Substitute.For<IBusinessLogic<TestModel>>();
        var interceptor2 = Substitute.For<IBusinessLogic<TestModel>>();
        var serviceProvider = TestingDefaults.ServiceCollection()
            .AddTransient<IBusinessLogic<TestModel>>(_ => interceptor1)
            .AddTransient<IBusinessLogic<TestModel>>(_ => interceptor2)
            .BuildServiceProvider();

        var businessLogicInterceptor = new BusinessLogicInterceptor(serviceProvider);
        await businessLogicInterceptor.InterceptBeforeAsync(model, ChangeState.Added, CancellationToken.None);
        await businessLogicInterceptor.InterceptAfterAsync(model, ChangeState.Added, CancellationToken.None);

        await interceptor1.Received(1).BeforeAsync(ChangeState.Added, model, CancellationToken.None);
        await interceptor1.Received(1).AfterAsync(ChangeState.Added, model, CancellationToken.None);
        await interceptor2.Received(1).BeforeAsync(ChangeState.Added, model, CancellationToken.None);
        await interceptor2.Received(1).AfterAsync(ChangeState.Added, model, CancellationToken.None);
    }

    [Test]
    public async Task BeforeAsyncHandlesUnknownState()
    {
        var model = TestModel.GetModel();
        var _businessLogic = Substitute.For<IBusinessLogic<TestModel>>();

        await _businessLogic.BeforeAsync((ChangeState)999, model, CancellationToken.None);
        await _businessLogic.Received(0).BeforeCreateAsync(model, CancellationToken.None);
        await _businessLogic.Received(0).BeforeUpdateAsync(model, CancellationToken.None);
        await _businessLogic.Received(0).BeforeDeleteAsync(model, CancellationToken.None);
    }



}
