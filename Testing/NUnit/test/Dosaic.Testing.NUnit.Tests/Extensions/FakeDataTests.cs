using AwesomeAssertions;
using Bogus;
using Dosaic.Testing.NUnit.Extensions;
using NUnit.Framework;

namespace Dosaic.Testing.NUnit.Tests.Extensions
{
    public class FakeDataTests
    {
        private readonly FakeData _fakeData = new(new() { Locale = "de" });

        [Test]
        public void CanCreateConfiguredFakes()
        {
            var data = FakeData.Instance.Fake<SampleClass>();
            data.Should().NotBeNull();
            data.Id.Should().NotBe(0);
            data.FirstName.Should().NotBeNullOrWhiteSpace();
            data.LastName.Should().NotBeNullOrWhiteSpace();
            data.FirstName.Should().NotBeNullOrWhiteSpace();
            data.FullName.Should().Be($"{data.FirstName} {data.LastName}");

            var id = 0;
            FakeData.Instance.Fakes<SampleClass>(10, x => x.Id = id++)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .Should()
                .BeEquivalentTo([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);

            FakeData.Instance.Fakes<SampleClass>(10, (f, x) => x.FullName = f.Date.Past().ToString("O"))
                .Should()
                .AllSatisfy(x => DateTime.TryParse(x.FullName, out _).Should().BeTrue());
        }

        [Test]
        public void CanCreateNonConfiguredFakes()
        {
            var data = _fakeData.Fake<SampleClass2>();
            data.Should().NotBeNull();
            data.FirstName.Should().BeNull();
            data.LastName.Should().BeNull();
            data.FullName.Should().BeNull();
            data.Id.Should().Be(0);

            var customData = _fakeData.Fake<SampleClass2>((f, x) =>
            {
                x.FirstName = f.Name.FirstName();
            });

            customData.FirstName.Should().NotBeNullOrWhiteSpace();
            data.LastName.Should().BeNull();
            data.FullName.Should().BeNull();
        }

        [Test]
        public void CanFakeMultiple()
        {
            var fakes = _fakeData.Fakes<SampleClass>(10);
            fakes.Should().HaveCount(10);

            fakes = _fakeData.Fakes<SampleClass>(10, x =>
            {
                x.FirstName = "test";
            });

            fakes.Should().HaveCount(10);
            fakes.Should().AllSatisfy(x => x.FirstName.Should().Be("test"));
        }
    }

    public class SampleClass
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
    }

    public class SampleClassFakeDataSetup : IFakeDataSetup<SampleClass>
    {
        public void ConfigureRules(Faker<SampleClass> faker)
        {
            faker.RuleFor(x => x.Id, f => f.IndexFaker + 1);
            faker.RuleFor(x => x.FirstName, f => f.Name.FirstName());
            faker.RuleFor(x => x.LastName, f => f.Name.LastName());
            faker.RuleFor(x => x.FullName, (f, x) => $"{x.FirstName} {x.LastName}");
        }
    }

    public class SampleClass2
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
    }
}
