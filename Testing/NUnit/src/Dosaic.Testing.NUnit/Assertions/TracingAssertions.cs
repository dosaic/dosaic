using System.Diagnostics;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using AwesomeAssertions.Primitives;
using Dosaic.Testing.NUnit.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Dosaic.Testing.NUnit.Assertions
{

    public static class TracingAssertionsExtensions
    {
        public static TracingAssertions Should(this IServiceProvider serviceProvider)
        {
            return new TracingAssertions(serviceProvider.GetRequiredService<TracerProvider>());
        }
    }

    public class TracingAssertions(TracerProvider provider) : ReferenceTypeAssertions<TracerProvider, TracingAssertions>(provider, AssertionChain.GetOrCreate())
    {
        protected override string Identifier => nameof(TracingAssertions);

        public AndConstraint<TracingAssertions> RegisterSources(string source, string because = "", params object[] becauseArgs) => RegisterSources([source], because, becauseArgs);

        public AndConstraint<TracingAssertions> RegisterSources(string[] sources, string because = "", params object[] becauseArgs)
        {
            var listener = Subject.GetInaccessibleValue<ActivityListener>("listener");
            var registeredSources = listener?.ShouldListenTo?.Target.GetInaccessibleValue<HashSet<string>>("activitySources") ?? [];

            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(sources.All(s => registeredSources?.Contains(s) ?? false))
                .FailWith($"Not all expected sources were registered. Expected: {{0}}. Registered: {{1}}", sources, registeredSources);

            return new AndConstraint<TracingAssertions>(this);
        }

        public AndConstraint<TracingAssertions> RegisterInstrumentation<T>(string because = "", params object[] becauseArgs)
        {
            var instrumentations = Subject.GetInaccessibleValue<List<object>>("instrumentations");

            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(instrumentations?.Any(i => i.GetType() == typeof(T)) ?? false)
                .FailWith($"Expected instrumentation of type {{0}} to be registered, but it was not.", typeof(T));

            return new AndConstraint<TracingAssertions>(this);
        }

        public AndConstraint<TracingAssertions> RegisterInstrumentation(string name, string because = "", params object[] becauseArgs)
        {
            var instrumentations = Subject.GetInaccessibleValue<List<object>>("instrumentations");

            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(instrumentations?.Any(i => i.GetType().Name == name) ?? false)
                .FailWith($"Expected instrumentation {{0}} to be registered, but it was not.", name);

            return new AndConstraint<TracingAssertions>(this);
        }
    }
}
