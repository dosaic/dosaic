using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Fabrics;

namespace Dosaic.Extensions.Tracing
{
    /// <summary>
    ///     Applies <see cref="TraceAttribute" /> across every project that references this package,
    ///     driven entirely by MSBuild properties (DosaicTracing*). No runtime DI involved.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TracingFabric : TransitiveProjectFabric
    {
        public override void AmendProject(IProjectAmender amender)
        {
            var mode = GetEnum(amender, "DosaicTracingMode", TracingMode.AllPublic);

            // AttributeOnly → apply nothing globally; consumers opt in with [Trace].
            if (mode == TracingMode.AttributeOnly)
                return;

            var defaultCapture = GetEnum(amender, "DosaicTracingCaptureArgs", ArgCaptureMode.None);
            var errorCapture = GetEnum(amender, "DosaicTracingCaptureArgsOnError", ArgCaptureMode.ToString);
            var includes = GetGlobs(amender, "DosaicTracingInclude");
            var excludes = GetGlobs(amender, "DosaicTracingExclude");

            amender
                .SelectTypes()
                .Where(t => !t.IsStatic
                    && t.TypeKind != TypeKind.Interface
                    && !HasAttribute(t, typeof(NoTraceAttribute))
                    && !HasAttribute(t, typeof(TraceAttribute))
                    && TracingGlob.Matches(t.FullName, includes, excludes))
                .SelectMany(t => t.Methods)
                .Where(m => m.MethodKind == MethodKind.Default
                    && !m.IsStatic
                    && !m.IsAbstract
                    && m.Name is not ("ToString" or "GetHashCode" or "Equals")
                    && IsIncludedByMode(m, mode)
                    && !HasAttribute(m, typeof(NoTraceAttribute))
                    && !HasAttribute(m, typeof(TraceAttribute)))
                .AddAspectIfEligible(_ => new TraceAttribute
                {
                    CaptureArgs = defaultCapture,
                    CaptureArgsOnError = errorCapture
                });
        }

        private static bool HasAttribute(IDeclaration declaration, Type attributeType)
            => declaration.Attributes.OfAttributeType(attributeType).Any();

        private static bool IsIncludedByMode(IMethod method, TracingMode mode) => mode switch
        {
            TracingMode.All => true,
            TracingMode.AllPublic => method.Accessibility == Accessibility.Public,
            TracingMode.PublicAsync => method.Accessibility == Accessibility.Public && IsAsyncReturn(method.ReturnType),
            _ => false
        };

        private static bool IsAsyncReturn(IType returnType)
            => returnType.IsConvertibleTo(typeof(Task))
            || returnType.IsConvertibleTo(typeof(ValueTask))
            || (returnType is INamedType nt
                && nt.IsGeneric
                && (nt.Definition.IsConvertibleTo(typeof(Task<>)) || nt.Definition.IsConvertibleTo(typeof(ValueTask<>))));

        private static T GetEnum<T>(IProjectAmender amender, string key, T fallback) where T : struct, Enum
            => amender.Project.TryGetProperty(key, out var raw)
                && !string.IsNullOrWhiteSpace(raw)
                && Enum.TryParse<T>(raw, true, out var value)
                ? value
                : fallback;

        private static string[]? GetGlobs(IProjectAmender amender, string key)
        {
            if (!amender.Project.TryGetProperty(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return null;
            var globs = new List<string>();
            foreach (var part in Regex.Split(raw, @"\s*;\s*"))
                if (part.Length > 0)
                    globs.Add(part);
            return globs.Count > 0 ? globs.ToArray() : null;
        }
    }
}
