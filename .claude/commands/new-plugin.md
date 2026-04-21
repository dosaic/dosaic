---
description: Scaffold a new Dosaic plugin under Plugins/<Category>/<Name>
argument-hint: "<Category> <Name>"
allowed-tools: Bash(dotnet sln:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(dotnet format:*), Read, Write, Edit, Glob, Grep
---

Delegate to the `plugin-author` subagent.

Input: `$ARGUMENTS` — expected two tokens `<Category> <Name>`.
If missing, ask user once before proceeding.

After scaffold, chain:
1. `format-guard` — verify formatting.
2. `dotnet-test-author` — author initial tests.
3. `/test` on the new test project.
