# Code Style & Conventions

## Formatting (from .editorconfig)
- **Indent:** 4 spaces for `.cs`, 2 for XML/JSON/YAML
- **Charset:** utf-8 (utf-8-bom for razor/cshtml)
- **Line endings:** LF for YAML and shell scripts, system default otherwise
- **Trailing whitespace:** trimmed
- **Final newline:** yes
- **Multiple blank lines:** disallowed (IDE2000 = warning)

## C# Style
- **`var` usage:** always (`csharp_style_var_*: suggestion`)
- **Namespace style:** block-scoped (`csharp_style_namespace_declarations = block_scoped`)
- **Braces:** not required for single-line (`csharp_prefer_braces = false`)
- **`this.` qualifier:** avoid
- **Throw expressions:** disallowed
- **Nullable:** disabled globally (`<Nullable>disable</Nullable>`)
- **Implicit usings:** disabled (explicit global usings for System, System.Collections.Generic, System.IO, System.Linq, System.Threading, System.Threading.Tasks)
- **Modifier order:** public, private, protected, internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async

## Naming
- **Fields (instance):** `_camelCase` prefix
- **Fields (static):** `_camelCase` prefix
- **Constants:** PascalCase
- **Locals/params:** camelCase
- **Local functions:** PascalCase
- **Everything else (types, methods, properties):** PascalCase

## Diagnostics as Warnings
Key CA rules enforced: CA1822 (make member static), CA2016 (forward CancellationToken), IDE0005 (remove unused usings), IDE0161 (file-scoped → block-scoped override), IDE0044 (make field readonly)

## Project Conventions
- Projects under `src/` and `test/` directories
- Test projects: `{ProjectName}.Tests`
- `InternalsVisibleTo` auto-configured for `{ProjectName}.Tests` and `DynamicProxyGenAssembly2`
- XML doc warnings suppressed (1591)
- All plugins reference `Dosaic.Hosting.Abstractions`
