---
description: Produce NuGet packages in Release (dry-run — no push)
argument-hint: "[project-path]"
allowed-tools: Bash(dotnet pack:*), Bash(dotnet restore:*), Bash(dotnet build:*)
---

```
dotnet pack ${1:-./Dosaic.sln} -c Release --nologo
```

Report produced `.nupkg` paths and their sizes. Never run `dotnet nuget push` — that is release-workflow only.
