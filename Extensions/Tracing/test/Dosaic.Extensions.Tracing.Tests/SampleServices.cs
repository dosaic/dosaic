
namespace Dosaic.Extensions.Tracing.Tests
{
    [Trace]
    public class SampleService
    {
        public string Echo(string value) => value;

        public async Task<int> AddAsync(int a, int b, CancellationToken ct = default)
        {
            await Task.Delay(1, ct);
            return a + b;
        }

        public void Boom(string reason) => throw new InvalidOperationException(reason);

        [NoTrace]
        public string Untraced(string value) => value;
    }

    [Trace(CaptureArgs = ArgCaptureMode.ToString)]
    public class ToStringCapturingService
    {
        public string Build(int id, [NoCapture] string secret, CancellationToken ct = default)
            => id + ":" + secret;
    }

    [Trace(CaptureArgs = ArgCaptureMode.Json)]
    public class JsonCapturingService
    {
        public string Handle(Payload payload) => payload.Name;

        public string HandleCyclic(Node node) => "ok";
    }

    public class ErrorCaptureService
    {
        [Trace(CaptureArgsOnError = ArgCaptureMode.Json)]
        public void BoomWithCyclic(Node node) => throw new InvalidOperationException("kaboom");
    }

    public record Payload(string Name, int Count);

    public class Node
    {
        public Node Self { get; set; }
    }

    public class MethodLevelService
    {
        [Trace]
        public string Traced(string value) => value;

        public string NotTraced(string value) => value;
    }
}
