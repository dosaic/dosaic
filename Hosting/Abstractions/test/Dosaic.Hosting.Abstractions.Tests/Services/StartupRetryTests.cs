using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Services
{
    [NonParallelizable]
    public class StartupRetryTests
    {
        private FakeLogger<StartupRetryTests> _logger;
        private IHostApplicationLifetime _lifetime;
        private int _calls;

        [SetUp]
        public void Init()
        {
            _logger = new FakeLogger<StartupRetryTests>();
            _lifetime = Substitute.For<IHostApplicationLifetime>();
            _calls = 0;
            Environment.ExitCode = 0;
        }

        [TearDown]
        public void Cleanup()
        {
            Environment.ExitCode = 0;
        }

        private Func<CancellationToken, Task> FailingTimes(int failures) => _ =>
        {
            _calls++;
            return _calls <= failures ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
        };

        private Task<bool> RunAsync(StartupRetryOptions options, Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            StartupRetry.RunAsync(options, "migrate things", action, _ => TimeSpan.Zero, _logger, _lifetime,
                cancellationToken);

        [Test]
        public async Task SucceedsOnTheFirstAttemptWithoutLogging()
        {
            var result = await RunAsync(new StartupRetryOptions(), FailingTimes(0));

            result.Should().BeTrue();
            _calls.Should().Be(1);
            _logger.Entries.Should().BeEmpty();
        }

        [Test]
        public async Task RetriesWithoutLimitByDefault()
        {
            var result = await RunAsync(null, FailingTimes(10));

            result.Should().BeTrue();
            _calls.Should().Be(11);
            _logger.Entries.Should().HaveCount(10);
            _logger.Entries[0].Message.Should().Be("Could not migrate things (attempt 1) -> retrying");
            _logger.Entries[9].Message.Should().Be("Could not migrate things (attempt 10) -> retrying");
            _logger.Entries.Should().AllSatisfy(x => x.Level.Should().Be(LogLevel.Error));
        }

        [Test]
        public async Task GivesUpAfterMaxAttemptsAndCountsThemRight()
        {
            var result = await RunAsync(new StartupRetryOptions { MaxAttempts = 3 }, FailingTimes(int.MaxValue));

            result.Should().BeFalse();
            _calls.Should().Be(3);
            _logger.Entries.Select(x => x.Message).Should().Equal(
                "Could not migrate things (attempt 1 of 3) -> retrying",
                "Could not migrate things (attempt 2 of 3) -> retrying",
                "Could not migrate things after 3 attempts -> giving up");
            _logger.Entries.Should().AllSatisfy(x => x.Exception.Should().BeOfType<InvalidOperationException>());
            _lifetime.DidNotReceive().StopApplication();
            Environment.ExitCode.Should().Be(0);
        }

        [Test]
        public async Task SucceedsOnTheLastAllowedAttempt()
        {
            var result = await RunAsync(new StartupRetryOptions { MaxAttempts = 3, StopHostOnFailure = true },
                FailingTimes(2));

            result.Should().BeTrue();
            _lifetime.DidNotReceive().StopApplication();
        }

        [Test]
        public async Task StopsTheHostWithExitCodeOneWhenStopHostOnFailureIsSet()
        {
            var result = await RunAsync(new StartupRetryOptions { MaxAttempts = 2, StopHostOnFailure = true },
                FailingTimes(int.MaxValue));

            result.Should().BeFalse();
            _calls.Should().Be(2);
            _logger.Entries.Last().Message.Should().Be("Could not migrate things after 2 attempts -> stopping the host");
            _logger.Entries.Last().Level.Should().Be(LogLevel.Critical);
            _lifetime.Received(1).StopApplication();
            Environment.ExitCode.Should().Be(1);
        }

        [Test]
        public async Task GivesUpWithoutALifetimeEvenWhenStopHostOnFailureIsSet()
        {
            var result = await StartupRetry.RunAsync(new StartupRetryOptions { MaxAttempts = 1, StopHostOnFailure = true },
                "migrate things", FailingTimes(int.MaxValue), _ => TimeSpan.Zero, _logger, null, CancellationToken.None);

            result.Should().BeFalse();
            _logger.Entries.Single().Message.Should().Be("Could not migrate things after 1 attempts -> giving up");
            Environment.ExitCode.Should().Be(0);
        }

        [Test]
        public async Task StopsRetryingWhenTheHostStops()
        {
            using var cts = new CancellationTokenSource();
            var result = await StartupRetry.RunAsync(new StartupRetryOptions(), "migrate things", _ =>
            {
                _calls++;
                cts.Cancel();
                throw new InvalidOperationException("boom");
            }, _ => TimeSpan.FromMinutes(1), _logger, _lifetime, cts.Token);

            result.Should().BeFalse();
            _calls.Should().Be(1);
            _lifetime.DidNotReceive().StopApplication();
        }

        [Test]
        public async Task ACancelledActionIsNoFailure()
        {
            using var cts = new CancellationTokenSource();
            var result = await StartupRetry.RunAsync(new StartupRetryOptions { MaxAttempts = 1, StopHostOnFailure = true },
                "migrate things", ct =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }, _ => TimeSpan.Zero, _logger, _lifetime, cts.Token);

            result.Should().BeFalse();
            _logger.Entries.Should().BeEmpty();
            _lifetime.DidNotReceive().StopApplication();
        }

        [Test]
        public async Task WaitsTheDelayOfTheFailedAttempt()
        {
            var delays = new List<int>();

            await StartupRetry.RunAsync(new StartupRetryOptions { MaxAttempts = 3 }, "migrate things",
                FailingTimes(int.MaxValue), attempt =>
                {
                    delays.Add(attempt);
                    return TimeSpan.Zero;
                }, _logger, _lifetime, CancellationToken.None);

            delays.Should().Equal(1, 2);
        }
    }
}
