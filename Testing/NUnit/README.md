# Dosaic.Testing.Nunit




## How to unit test metric collection

```csharp
using var metricsCollector = new TestMetricsCollector("my-metric-name");
metricsCollector.CollectedMetrics.Should().BeEmpty();

// do test stuff e.g. call method, etc..

metricsCollector.Instruments.Should().Contain("my-metric-name");
metricsCollector.CollectedMetrics.Should().ContainsMetric(1);
```
