using AwesomeAssertions;
using Dosaic.Testing.NUnit.Extensions;
using NUnit.Framework;

namespace Dosaic.DevTools.Seeding.Tests
{
    public class FakeDataTests
    {
        [Test]
        public void FakeWithSeedProducesDeterministicData()
        {
            var config = new FakeDataConfig { Seed = 12345 };
            var fakeData1 = new FakeData(config);
            var fakeData2 = new FakeData(config);

            var customers1 = fakeData1.Fakes<Customer>(5);
            var customers2 = fakeData2.Fakes<Customer>(5);

            customers1.Should().HaveCount(5);
            for (var i = 0; i < customers1.Count; i++)
            {
                customers1[i].Name.Should().Be(customers2[i].Name);
                customers1[i].State.Should().Be(customers2[i].State);
            }
        }

        [Test]
        public void FakeWithSeedProducesDeterministicSingleItem()
        {
            var config = new FakeDataConfig { Seed = 99 };
            var fakeData1 = new FakeData(config);
            var fakeData2 = new FakeData(config);

            var product1 = fakeData1.Fake<Product>();
            var product2 = fakeData2.Fake<Product>();

            product1.Name.Should().Be(product2.Name);
            product1.Price.Should().Be(product2.Price);
        }

        [Test]
        public void FakeWithoutSeedCanProduceDifferentData()
        {
            var fakeData1 = new FakeData(new FakeDataConfig());
            var fakeData2 = new FakeData(new FakeDataConfig());

            var names1 = fakeData1.Fakes<Customer>(20).Select(c => c.Name).ToList();
            var names2 = fakeData2.Fakes<Customer>(20).Select(c => c.Name).ToList();

            // Without a seed, two independent runs are very unlikely to match
            names1.Should().NotBeEquivalentTo(names2);
        }

        [Test]
        public void DifferentSeedsProduceDifferentData()
        {
            var fakeData1 = new FakeData(new FakeDataConfig { Seed = 1 });
            var fakeData2 = new FakeData(new FakeDataConfig { Seed = 2 });

            var customer1 = fakeData1.Fake<Customer>();
            var customer2 = fakeData2.Fake<Customer>();

            customer1.Name.Should().NotBe(customer2.Name);
        }
    }
}
