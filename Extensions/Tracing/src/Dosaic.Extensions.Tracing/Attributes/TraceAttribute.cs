using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Extensions;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Eligibility;

namespace Dosaic.Extensions.Tracing
{
    /// <summary>
    ///     Weaves an OpenTelemetry activity around the target method at compile time.
    ///     When applied to a class, every eligible instance method of that class is traced.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    [ExcludeFromCodeCoverage]
    public class TraceAttribute : OverrideMethodAspect, IAspect<INamedType>
    {
        /// <summary>Parameter capture strategy on the happy path. Default: None.</summary>
        public ArgCaptureMode CaptureArgs { get; set; } = ArgCaptureMode.None;

        /// <summary>
        ///     Parameter capture strategy applied only when the method throws. Default: ToString.
        ///     Ignored when <see cref="CaptureArgs" /> already captures arguments (to avoid duplicate tags).
        /// </summary>
        public ArgCaptureMode CaptureArgsOnError { get; set; } = ArgCaptureMode.ToString;

        // ── Class target: cascade to every eligible method ──

        public void BuildAspect(IAspectBuilder<INamedType> builder)
            => builder.Outbound
                .SelectMany(t => t.Methods)
                .Where(m => m.MethodKind == MethodKind.Default
                    && !m.IsStatic
                    && !m.IsAbstract
                    && m.Name is not ("ToString" or "GetHashCode" or "Equals")
                    && !m.Attributes.OfAttributeType(typeof(NoTraceAttribute)).Any()
                    && !m.Attributes.OfAttributeType(typeof(TraceAttribute)).Any())
                .AddAspectIfEligible(_ => new TraceAttribute
                {
                    CaptureArgs = CaptureArgs,
                    CaptureArgsOnError = CaptureArgsOnError
                });

        public void BuildEligibility(IEligibilityBuilder<INamedType> builder)
        {
        }

        // ── Method target: wrap the body ──

        public override dynamic? OverrideMethod()
        {
            var spanName = $"{meta.Target.Type.Name}.{meta.Target.Method.Name}";
            using Activity? activity = Dosaic.Hosting.Abstractions.Tracing.StartActivity(spanName);
            CaptureParameters(activity, CaptureArgs);
            try
            {
                return meta.Proceed();
            }
            catch (Exception ex)
            {
                if (CaptureArgs == ArgCaptureMode.None)
                    CaptureParameters(activity, CaptureArgsOnError);
                activity?.SetErrorStatus(ex);
                throw;
            }
        }

        public override async Task<dynamic?> OverrideAsyncMethod()
        {
            var spanName = $"{meta.Target.Type.Name}.{meta.Target.Method.Name}";
            using Activity? activity = Dosaic.Hosting.Abstractions.Tracing.StartActivity(spanName);
            CaptureParameters(activity, CaptureArgs);
            try
            {
                return await meta.ProceedAsync();
            }
            catch (Exception ex)
            {
                if (CaptureArgs == ArgCaptureMode.None)
                    CaptureParameters(activity, CaptureArgsOnError);
                activity?.SetErrorStatus(ex);
                throw;
            }
        }

        [Template]
        private static void CaptureParameters(Activity? activity, [CompileTime] ArgCaptureMode mode)
        {
            if (mode != ArgCaptureMode.None)
            {
                foreach (var p in meta.Target.Parameters)
                {
                    if (!ShouldSkip(p))
                    {
                        // Capturing must never throw — a serialization failure here would otherwise
                        // mask the method's real exception on the error path.
                        try
                        {
                            if (mode == ArgCaptureMode.ToString)
                            {
                                // Reference / Nullable<T> arguments may be null; value types never are.
                                if (p.Type.IsReferenceType != false || p.Type.IsNullable == true)
                                    activity?.SetTag($"arg.{p.Name}", p.Value?.ToString());
                                else
                                    activity?.SetTag($"arg.{p.Name}", p.Value.ToString());
                            }
                            else if (mode == ArgCaptureMode.Json)
                            {
                                activity?.SetTag($"arg.{p.Name}", SerializationExtensions.Serialize(p.Value));
                            }
                        }
                        catch (Exception captureError)
                        {
                            activity?.SetTag($"arg.{p.Name}", "<" + captureError.GetType().Name + ">");
                        }
                    }
                }
            }
        }

        /// <summary>Compile-time filter: types that are never useful (or unsafe) as span tags.</summary>
        private static bool ShouldSkip(IParameter p)
            => p.Type.IsConvertibleTo(typeof(CancellationToken))
            || p.Type.IsConvertibleTo(typeof(Stream))
            || p.Type.IsConvertibleTo(typeof(byte[]))
            || p.Type.IsConvertibleTo(typeof(ReadOnlyMemory<byte>))
            || p.Type.IsConvertibleTo(typeof(IServiceProvider))
            || p.Type.IsConvertibleTo(typeof(ClaimsPrincipal))
            || IsNamed(p.Type, "Microsoft.AspNetCore.Http.HttpContext")
            || IsNamed(p.Type, "Microsoft.AspNetCore.Http.IFormFile")
            || p.Attributes.OfAttributeType(typeof(NoCaptureAttribute)).Any();

        private static bool IsNamed(IType type, string fullName)
            => type is INamedType named && named.FullName == fullName;
    }
}
