# Task Completion Checklist

When completing a coding task in the Dosaic repository:

## Before Submitting
1. **Format check:** `dotnet format --verify-no-changes --no-restore`
2. **Build check:** `dotnet build -c Release`
3. **Test run:** `dotnet test ./Dosaic.sln` (or specific test project)
4. **Coverage:** Ensure 80%+ line coverage for new/modified code

## Code Quality
- Follow `.editorconfig` rules (block-scoped namespaces, `var` everywhere, `_camelCase` fields)
- Ensure `Nullable` is disabled (project default)
- No Arrange/Act/Assert comments in tests
- Use AwesomeAssertions (not FluentAssertions) for test assertions
- Use NSubstitute (not Moq) for mocking
- CamelCase test method names (no underscores)

## Plugin Changes
- Plugin must implement one or more of the `IPlugin*` interfaces
- Plugin project must reference `Dosaic.Hosting.Abstractions`
- Test project must be named `{ProjectName}.Tests`
- Verify plugin is discoverable by the source generator (public, non-abstract, implements IPluginActivateable)
- Ensure backward compatibility or update all references (`find_referencing_symbols`)

## CI Expectations
- .NET 10.0.x on Ubuntu
- Format → Build → Test → Coverage pipeline
- NuGet packages published on GitHub release
