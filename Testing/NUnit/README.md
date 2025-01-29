# Dosaic.Testing.Nunit

This plugin is designed to have a "all-in-one"-package for testing, so you will basically have more packages on the Tests installed as needed, but can start using it without searching the packages.

## How to unit test metric collection

```csharp
using var metricsCollector = new TestMetricsCollector("my-metric-name");
metricsCollector.CollectedMetrics.Should().BeEmpty();

// do test stuff e.g. call method, etc..

metricsCollector.Instruments.Should().Contain("my-metric-name");
metricsCollector.CollectedMetrics.Should().ContainsMetric(1);
```
