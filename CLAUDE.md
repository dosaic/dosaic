# CLAUDE.md

Canonical AI guidance lives in [AGENTS.md](AGENTS.md). Read it first — it covers project layout, plugin architecture, code style, testing stack, and CI commands.

## Claude Code specifics

### MCP servers
`.mcp.json` at repo root declares:
- **context7** (HTTP) — library docs lookup
- **serena** (stdio) — symbol-level code nav, prefer over raw file reads when navigating

Enable via `/mcp` if not auto-loaded.

### Subagents (`.claude/agents/`)
| Agent | Use for |
|---|---|
| `plugin-author` | scaffold a new Dosaic plugin |
| `dotnet-test-author` | author NUnit tests with AwesomeAssertions + NSubstitute |
| `format-guard` | verify / auto-fix `dotnet format` |
| `coverage-analyst` | run coverage pass, flag modules under 80% |
| `dependency-auditor` | NuGet outdated + vulnerable triage |
| `architecture-reviewer` | invariant punch-list before PR |

### Slash commands (`.claude/commands/`)
`/build` · `/test` · `/format` · `/coverage` · `/pack` · `/deps` · `/new-plugin <Category> <Name>` · `/pr-review`

### Non-negotiables
- `var` everywhere; block-scoped namespaces; braces only for multi-line
- `Nullable` disabled repo-wide
- NUnit 4 + **AwesomeAssertions** (not FluentAssertions) + **NSubstitute** (not Moq)
- `NUnit3TestAdapter` pinned to v5 — do not bump
- Central package versions via `Directory.Packages.props` — no inline `Version=` on `PackageReference`
- Coverage gate ≥80% line, enforced in CI
- `dotnet format --verify-no-changes --no-restore` must pass before merge
