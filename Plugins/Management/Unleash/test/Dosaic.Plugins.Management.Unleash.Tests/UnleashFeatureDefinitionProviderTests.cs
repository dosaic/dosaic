using AwesomeAssertions;
using Microsoft.FeatureManagement;
using NSubstitute;
using NUnit.Framework;
using Unleash;

namespace Dosaic.Plugins.Management.Unleash.Tests
{
    [TestFixture]
    public class UnleashFeatureDefinitionProviderTests
    {
        [Test]
        public async Task FeatureNameMisMatchReturnsNull()
        {
            var unleash = Substitute.For<IUnleash>();
            var featureToggles = new List<ToggleDefinition>()
            {
                new("test", "default", FeatureToggleType.Release)
            };
            unleash.ListKnownToggles().Returns(featureToggles);
            var unleashFeatureDefinitionProvider = new UnleashFeatureDefinitionProvider(unleash);
            var featureDefinition = await unleashFeatureDefinitionProvider.GetFeatureDefinitionAsync("myfancyFeature");

            featureDefinition.Should().BeNull();
        }

        [Test]
        public async Task FeatureWithOneStrategyShouldHaveRequirementTypeAll()
        {
            var unleash = Substitute.For<IUnleash>();
            var featureToggles = new List<ToggleDefinition>()
            {
                new("test", "default", FeatureToggleType.Release)
            };
            unleash.ListKnownToggles().Returns(featureToggles);
            var unleashFeatureDefinitionProvider = new UnleashFeatureDefinitionProvider(unleash);
            var featureDefinition = await unleashFeatureDefinitionProvider.GetFeatureDefinitionAsync("test");

            featureDefinition.Name.Should().Be(featureToggles[0].Name);
            featureDefinition.EnabledFor.Should().Satisfy(x => x.Name == UnleashFilter.FilterAlias);
            featureDefinition.RequirementType.Should().Be(RequirementType.All);
        }

        [Test]
        public async Task GetAllFeatureDefinitionsAsyncShouldReturnCorrectFeatureDefinitons()
        {
            var unleash = Substitute.For<IUnleash>();
            var featureToggles = new List<ToggleDefinition>()
            {
                new("test","default", FeatureToggleType.Release ),
                new("another-one","default", FeatureToggleType.Experiment ),
            };
            unleash.ListKnownToggles().Returns(featureToggles);
            var unleashFeatureDefinitionProvider = new UnleashFeatureDefinitionProvider(unleash);
            var featureDefinition = unleashFeatureDefinitionProvider.GetAllFeatureDefinitionsAsync();

            var enumerator = featureDefinition.GetAsyncEnumerator();
            await enumerator.MoveNextAsync();
            enumerator.Current.Name.Should().Be(featureToggles[0].Name);
            enumerator.Current.EnabledFor.Should().Satisfy(x => x.Name == UnleashFilter.FilterAlias);
            enumerator.Current.RequirementType.Should().Be(RequirementType.All);

            await enumerator.MoveNextAsync();
            enumerator.Current.Name.Should().Be(featureToggles[1].Name);
            enumerator.Current.EnabledFor.Should().Satisfy(x => x.Name == UnleashFilter.FilterAlias);
            enumerator.Current.RequirementType.Should().Be(RequirementType.All);
        }
    }
}
