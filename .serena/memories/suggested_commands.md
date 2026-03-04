# Suggested Commands

## Build & Run
```bash
# Restore packages
dotnet restore

# Build solution
dotnet build ./Dosaic.sln

# Build in Release mode
dotnet build -c Release

# Run the example service
dotnet run --project example/src/Dosaic.Example.Service
```

## Testing
```bash
# Run all tests
dotnet test ./Dosaic.sln

# Run tests with coverage
dotnet test --collect "Code Coverage;Format=Xml;CoverageFileName=coverage.xml" --results-directory "./test-results" --no-restore --nologo -c Release --logger trx

# Run tests for a specific project
dotnet test Plugins/Mapping/Mapster/test/Dosaic.Plugins.Mapping.Mapster.Tests
```

## Formatting & Linting
```bash
# Check formatting (no changes)
dotnet format --verify-no-changes --no-restore

# Auto-fix formatting
dotnet format
```

## Packaging
```bash
# Pack NuGet packages
dotnet pack -c Release

# Package utility script
bash packages.sh -b  # Generate NuGet badge markdown
bash packages.sh -p  # Generate Directory.Packages.props entries
bash packages.sh -j  # Dump package list as JSON
bash packages.sh -n  # Dump package names
```

## Git
```bash
git status
git diff
git log --oneline -20
git branch -a
```

## System
```bash
ls -la
find . -name "*.cs" | head -20
grep -r "pattern" --include="*.cs"
```
