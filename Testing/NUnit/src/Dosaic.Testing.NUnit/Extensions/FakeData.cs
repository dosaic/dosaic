using System.Collections.Concurrent;
using Bogus;
using Dosaic.Hosting.Abstractions.Extensions;

namespace Dosaic.Testing.NUnit.Extensions
{
    public class FakeDataConfig
    {
        public string Locale { get; set; } = "en";
        public bool UseStrictMode { get; set; }
    }

    public class FakeData
    {
        private readonly FakeDataConfig _config;
        private readonly Faker _faker;
        private readonly ConcurrentDictionary<Type, object> _typedFakers = new();
        private readonly IList<object> _testDataSetups;

        public FakeData(FakeDataConfig config)
        {
            _config = config;
            _faker = new Faker(config.Locale);
            _testDataSetups = AppDomain.CurrentDomain
                .GetAssemblies()
                .GetTypesSafely(t => t.IsClass && !t.IsAbstract && t.Implements(typeof(IFakeDataSetup<>)))
                .Select(Activator.CreateInstance)
                .ToList();
        }

        public FakeData() : this(new FakeDataConfig()) { }

        public static FakeData Instance { get; private set; } = new();
        public static void ConfigureInstance(FakeDataConfig config) =>
            Instance = new FakeData(config);

        private Faker<T> GetFaker<T>() where T : class
        {
            return _typedFakers.GetOrAdd(typeof(T), _ => CreateFaker<T>()) as Faker<T>;
        }

        public Faker Faker => _faker;

        public Faker<T> CreateFaker<T>() where T : class
        {
            var typedFaker = new Faker<T>(_faker.Locale);
            typedFaker.StrictMode(_config.UseStrictMode);
            _testDataSetups
                .OfType<IFakeDataSetup<T>>()
                .ForEach(x => x.ConfigureRules(typedFaker));
            if (_config.UseStrictMode)
                typedFaker.AssertConfigurationIsValid();
            return typedFaker;
        }

        public T Fake<T>(Action<Faker, T> configure) where T : class
        {
            var faker = GetFaker<T>();
            var fake = faker.Generate();
            configure?.Invoke(_faker, fake);
            return fake;
        }

        public T Fake<T>(Action<T> configure) where T : class => Fake<T>(configure is null ? null : (_, t) => configure(t));
        public T Fake<T>() where T : class => Fake((Action<T>)null);

        public List<T> Fakes<T>(int count, Action<Faker, T> configure) where T : class
        {
            var faker = GetFaker<T>();
            var fakes = faker.Generate(count);
            if (configure is null) return fakes;
            fakes.ForEach(x => configure.Invoke(_faker, x));
            return fakes;
        }

        public List<T> Fakes<T>(int count, Action<T> configure) where T : class => Fakes<T>(count, configure is null ? null : (_, t) => configure(t));
        public List<T> Fakes<T>(int count) where T : class => Fakes(count, (Action<T>)null);
    }

    public interface IFakeDataSetup<T> where T : class
    {
        void ConfigureRules(Faker<T> faker);
    }
}
