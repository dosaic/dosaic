---
description: Verify + apply dotnet format. Mirrors CI gate.
allowed-tools: Bash(dotnet format:*), Bash(git status:*), Bash(git diff:*)
---

1. `dotnet format --verify-no-changes --no-restore` — capture result.
2. If violations present: `dotnet format` then re-verify.
3. Show `git diff --stat` of fixes (if any).

Delegate deeper analysis to the `format-guard` subagent when violations persist after auto-fix.
