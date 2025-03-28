using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dosaic.Hosting.Generator
{
    [Generator]
    [ExcludeFromCodeCoverage]
    public class PluginTypesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var plugins = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: (node, _) => node is TypeDeclarationSyntax,
                transform: (ctx, _) => ctx.Node as BaseTypeDeclarationSyntax);
            var compilationModel = context.CompilationProvider.Combine(plugins.Collect());

            context.RegisterSourceOutput(compilationModel, (sourceContext, source) => Execute(source.Left, sourceContext));
        }

        private static void Execute(Compilation compilation, SourceProductionContext sourceContext)
        {
            var assembly = typeof(PluginTypesGenerator).Assembly.GetName();
            var name = assembly.Name;
            var version = assembly.Version.ToString();
            var classes = GetPluginClasses(compilation);
            var sourceBuilder = new StringBuilder();
            sourceBuilder.Clear();
            sourceBuilder.AppendLine("using System;");
            sourceBuilder.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sourceBuilder.AppendLine("using System.CodeDom.Compiler;");
            sourceBuilder.AppendLine("namespace Dosaic.Generated;");
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("[ExcludeFromCodeCoverage]");
            sourceBuilder.AppendLine($"[GeneratedCode(\"{name}\", \"{version}\")]");
            sourceBuilder.AppendLine("public class DosaicPluginTypes {");
            if (classes.Any())
            {
                sourceBuilder.AppendLine(" public static Type[] All = new Type[] {");
                foreach (var pluginClass in classes)
                {
                    sourceBuilder.AppendLine($"  {pluginClass.TypeOfName},");
                }

                sourceBuilder.AppendLine(" };");
            }
            else
            {
                sourceBuilder.AppendLine(" public static Type[] All = new Type[0];");
            }

            sourceBuilder.AppendLine("}");
            sourceContext.AddSource("DosaicPluginTypes.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }

        private static IList<PluginClass> GetPluginClasses(Compilation compilation)
        {
            var mainTypes = GetAssemblySymbolTypes(compilation.SourceModule.ContainingAssembly);
            var referencedTypes = compilation.SourceModule.ReferencedAssemblySymbols.SelectMany(GetAssemblySymbolTypes);
            var typeSymbols = mainTypes.Concat(referencedTypes).Where(t =>
                !t.IsAbstract &&
                !_excludes.Any(n => GetRootNamespaceSymbolFor(t).Name.StartsWith(n, StringComparison.OrdinalIgnoreCase)) &&
                t.DeclaredAccessibility == Accessibility.Public
                && ImplementsDosaicClasses(t)
            ).ToList();

            return typeSymbols.Select(GetPluginClass).OrderBy(x => x.FullName).ToList();
        }

        private static PluginClass GetPluginClass(ITypeSymbol typeSymbol)
        {
            var ns = typeSymbol.ContainingNamespace.ToDisplayString();
            var interfaces = typeSymbol.AllInterfaces
                .Where(x => x.AllInterfaces.Any(t => t.Name == "IPluginActivateable"))
                .Select(s => s.Name)
                .ToList();
            var typeOfName = $"typeof({ns}.{typeSymbol.Name})";
            if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType && namedTypeSymbol.TypeArguments.Length > 0)
                typeOfName = $"typeof({ns}.{namedTypeSymbol.Name}<{new string(',', namedTypeSymbol.TypeArguments.Length - 1)}>)";
            return new PluginClass
            {
                Name = typeSymbol.Name,
                Namespace = ns,
                Interfaces = interfaces,
                TypeOfName = typeOfName
            };
        }

        private static bool ImplementsDosaicClasses(ITypeSymbol t)
        {
            if (t.AllInterfaces.Any(x => x.Name == "IPluginActivateable"))
                return true;
            if (t.GetAttributes().Any(
                    x => x.AttributeClass != null
                         && GetRootNamespaceSymbolFor(x.AttributeClass).Name.StartsWith("Dosaic", StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        private static readonly string[] _excludes =
        {
            "Microsoft", "System", "FastEndpoints", "testhost", "netstandard", "Newtonsoft", "mscorlib", "NuGet", "NSwag", "FluentValidation", "YamlDotNet", "Accessibility",
            "NJsonSchema", "Namotion"
        };

        private static INamespaceSymbol GetRootNamespaceSymbolFor(ISymbol symbol)
        {
            var currentNamespace = symbol.ContainingNamespace;

            while (true)
            {
                var parentNamespace = currentNamespace.ContainingNamespace;
                if (parentNamespace?.IsGlobalNamespace != false)
                {
                    return currentNamespace;
                }

                currentNamespace = parentNamespace;
            }
        }

        private static IEnumerable<ITypeSymbol> GetAllTypes(INamespaceSymbol root)
        {
            foreach (var namespaceOrTypeSymbol in root.GetMembers())
            {
                switch (namespaceOrTypeSymbol)
                {
                    case INamespaceSymbol @namespace:
                        {
                            foreach (var nested in GetAllTypes(@namespace))
                            {
                                yield return nested;
                            }
                            break;
                        }
                    case ITypeSymbol type:
                        {
                            yield return type;
                            break;
                        }
                }
            }
        }

        private static IEnumerable<ITypeSymbol> GetAssemblySymbolTypes(IAssemblySymbol a) => GetAllTypes(a.GlobalNamespace);
    }

    [ExcludeFromCodeCoverage]
    public class PluginClass
    {
        public string FullName => $"{Namespace}.{Name}";
        public string Name { get; set; }
        public string TypeOfName { get; set; }
        public string Namespace { get; set; }
        public IList<string> Interfaces { get; set; }
    }
}
