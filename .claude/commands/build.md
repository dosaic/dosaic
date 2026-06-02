---
description: Restore + build the full solution in Release config
argument-hint: "[project-path]"
allowed-tools: Bash(dotnet restore:*), Bash(dotnet build:*)
---

Build target: `$1` if provided, else `./Dosaic.slnx`.

Run:
```
dotnet restore ${1:-./Dosaic.slnx}
dotnet build ${1:-./Dosaic.slnx} -c Release --no-restore
```

Report: elapsed time, warning count, error count. If errors, quote the first 5 exactly.
