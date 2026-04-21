---
description: Run tests for the solution or a specific test project
argument-hint: "[test-project-path]"
allowed-tools: Bash(dotnet test:*), Bash(dotnet restore:*)
---

Run `dotnet test` against `$1` if given, else `./Dosaic.sln`.

```
dotnet test ${1:-./Dosaic.sln} --nologo --logger "console;verbosity=minimal"
```

Report pass/fail/skip counts. Quote the first 3 failing test names + one-line failure reason each. Do not propose fixes unless asked.
