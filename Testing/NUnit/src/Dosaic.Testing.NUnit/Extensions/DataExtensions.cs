using AutoBogus;

namespace Dosaic.Testing.NUnit.Extensions
{
    public static class TestData
    {
        public static T Fake<T>(Action<T> configure = null) where T : class
        {
            var result = new AutoFaker<T>().Configure(c => c.WithRecursiveDepth(0)).Generate();
            configure?.Invoke(result);
            return result;
        }

        public static T[] FakeMany<T>(int count, Action<T> configure = null) where T : class
        {
            var result = new AutoFaker<T>().Configure(c => c.WithRecursiveDepth(0)).Generate(count);
            foreach (var r in result)
                configure?.Invoke(r);
            return result.ToArray();
        }

        public static T FakeDeep<T>(int depth = 1, Action<T> configure = null) where T : class
        {
            var result = new AutoFaker<T>().Configure(c => c.WithRecursiveDepth(depth)).Generate();
            configure?.Invoke(result);
            return result;
        }

        public static T[] FakeDeepMany<T>(int count, int depth = 1, Action<T> configure = null) where T : class
        {
            var result = new AutoFaker<T>().Configure(c => c.WithRecursiveDepth(depth)).Generate(count);
            foreach (var r in result)
                configure?.Invoke(r);
            return result.ToArray();
        }
    }
}
