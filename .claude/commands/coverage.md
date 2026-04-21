---
description: Run coverage-instrumented test pass and report against the 80% gate
allowed-tools: Bash(dotnet test:*), Read, Glob
---

Delegate to the `coverage-analyst` subagent. Pass through any extra args in `$ARGUMENTS` as additional scope (e.g. a specific test project).
