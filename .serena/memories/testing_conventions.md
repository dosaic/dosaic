# Testing Conventions

## Framework
- **NUnit** 4.5.0 as test runner
- **NUnit3TestAdapter** 5.2.0 (DO NOT upgrade to v6!)
- **AwesomeAssertions** 9.4.0 for fluent assertions (`.Should()`)
- **NSubstitute** 5.3.0 for mocking
- **Bogus** / **AutoBogus** for fake data generation
- **WireMock.Net** and **RichardSzalay.MockHttp** for HTTP mocking
- **TngTech.ArchUnitNET.NUnit** for architecture tests
- **Allure.NUnit** for test reporting

## Test Style Rules
1. **CamelCase** test method names (no underscores)
2. **No** Arrange/Act/Assert comments
3. Test name should be self-explanatory
4. Use `AwesomeAssertions` not FluentAssertions
5. Use `NSubstitute` for mocking (not Moq)
6. Test projects auto-marked `[Parallelizable(ParallelScope.Fixtures)]` and `[ExcludeFromCodeCoverage]`

## Test Helpers (Dosaic.Testing.NUnit)
- `FakeLogger<T>` — captures log entries for assertion
- `TestingDefaults` — common test configuration
- `ActivityTestBootstrapper` — OpenTelemetry test setup
- `TestMetricsCollector` — metrics assertion helpers

## Test Pattern Example
```csharp
[TestFixture]
public class MyPluginTests
{
    private IImplementationResolver _resolver;
    private MyPlugin _plugin;

    [SetUp]
    public void SetUp()
    {
        _resolver = Substitute.For<IImplementationResolver>();
        _resolver.FindAssemblies().Returns(new List<Assembly> { typeof(MyPluginTests).Assembly });
        _plugin = new MyPlugin(_resolver);
    }

    [Test]
    public void ConfigureServicesShouldRegisterDependencies()
    {
        var services = new ServiceCollection();
        _plugin.ConfigureServices(services);
        services.Should().Contain(x => x.ServiceType == typeof(IMyService));
    }
}
```

## Coverage
- 80% line coverage threshold enforced in CI
- Code coverage collected via `dotnet test --collect "Code Coverage"`
