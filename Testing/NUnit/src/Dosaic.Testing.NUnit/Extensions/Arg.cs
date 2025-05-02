using FluentAssertions.Execution;
using NSubstitute.Core;
using NSubstitute.Core.Arguments;
using NUnit.Framework;

namespace Dosaic.Testing.NUnit.Extensions;

public static class ArgExt
{
    public static T That<T>(Action<T> action)
    {
        return ArgumentMatcher.Enqueue<T>(new AssertionMatcher<T>(action));
    }

    public class AssertionMatcher<T> : IArgumentMatcher<T>, IDescribeNonMatches
    {
        private readonly Action<T> _assertion;

        public AssertionMatcher(Action<T> assertion)
        {
            _assertion = assertion;
        }

        public bool IsSatisfiedBy(T argument)
        {
            using var scope = new AssertionScope();
            _assertion((T)argument);

            var failures = scope.Discard().ToList();

            if (failures.Count == 0)
            {
                return true;
            }

            failures.ForEach(x => TestContext.Error.WriteLine("error " + x));
            failures.ForEach(x => Console.WriteLine(x));

            return false;
        }

        public string DescribeFor(object argument)
        {
            try
            {
                using var scope = new AssertionScope();
                _assertion((T)argument);

                var failures = scope.Discard().ToList();

                if (failures.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join(Environment.NewLine, failures);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
