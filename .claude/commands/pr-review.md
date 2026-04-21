---
description: Run architecture + format + coverage review on the current branch vs main
allowed-tools: Bash(git diff:*), Bash(git log:*), Bash(git status:*), Bash(gh pr:*), Bash(dotnet format:*), Bash(dotnet test:*), Read, Grep, Glob
---

1. Summarize branch vs `main`:
   - `git log --oneline main..HEAD`
   - `git diff --stat main..HEAD`
2. Delegate to `architecture-reviewer` with the diff in scope.
3. Delegate to `format-guard`.
4. Optionally delegate to `coverage-analyst` if source in `Plugins/` or `Hosting/` changed.

Consolidate findings into a single punch-list with file:line references. Do not auto-apply fixes.
