---
description: Audit NuGet dependencies (outdated + vulnerable) against Directory.Packages.props
allowed-tools: Bash(dotnet list:*), Bash(dotnet restore:*), Read, Grep, Glob
---

Delegate to the `dependency-auditor` subagent. Respect repo pins:
- `MassTransit` / `MassTransit.RabbitMQ` stay on v8.x (v9+ is commercially licensed).
- `AwesomeAssertions` only (never FluentAssertions).
- `NSubstitute` only (never Moq).
