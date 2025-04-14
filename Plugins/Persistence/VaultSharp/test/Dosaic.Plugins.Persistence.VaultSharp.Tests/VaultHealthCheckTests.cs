using Dosaic.Testing.NUnit.Assertions;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using VaultSharp;
using VaultSharp.V1;
using VaultSharp.V1.SystemBackend;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Dosaic.Plugins.Persistence.VaultSharp.Tests;

public class VaultHealthCheckTests
{
    private IHealthCheck _healthCheck;
    private ISystemBackend _vaultBackend;

    [SetUp]
    public void Setup()
    {
        _vaultBackend = Substitute.For<ISystemBackend>();
        var v1Client = Substitute.For<IVaultClientV1>();
        v1Client.System.Returns(_vaultBackend);
        var vaultClient = Substitute.For<IVaultClient>();
        vaultClient.V1.Returns(v1Client);
        _healthCheck = new VaultHealthCheck(vaultClient, new FakeLogger<VaultHealthCheck>());
    }

    [Test]
    public async Task ReportsUnhealthyOnException()
    {
        _vaultBackend.GetHealthStatusAsync().ThrowsAsync(new Exception("test"));
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());
        result.Exception.Should().BeNull();
        result.Description.Should().Be("Failure checking vault health");
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task ReportsUnhealthyOnWrongStatusCode()
    {
        _vaultBackend.GetHealthStatusAsync()
            .Returns(new global::VaultSharp.V1.SystemBackend.HealthStatus { HttpStatusCode = 503 });
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());
        result.Exception.Should().BeNull();
        result.Description.Should().Contain("failed with status code 503");
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task ReportsHealthyWhenStatusCodeIs200()
    {
        _vaultBackend.GetHealthStatusAsync()
            .Returns(new global::VaultSharp.V1.SystemBackend.HealthStatus { HttpStatusCode = 200 });
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());
        result.Status.Should().Be(HealthStatus.Healthy);
    }
}
