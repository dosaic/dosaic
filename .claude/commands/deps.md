---
description: Audit NuGet dependencies (outdated + vulnerable) against Directory.Packages.props
allowed-tools: Bash(dotnet list:*), Bash(dotnet restore:*), Read, Grep, Glob
---

Delegate to the `dependency-auditor` subagent. Respect repo pins:
- `NUnit3TestAdapter` stays on v5.x.
- `AwesomeAssertions` only (never FluentAssertions).
- `NSubstitute` only (never Moq).
