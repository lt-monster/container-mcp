# Release Notes Rules

Use these rules when writing, revising, or reviewing GitHub Release notes for `container-mcp`.

## Source of Truth

Generate release notes from the actual release diff:

```powershell
git log --oneline <previous-tag>..<new-tag>
git diff --stat <previous-tag>..<new-tag>
git diff --name-only <previous-tag>..<new-tag>
```

Do not invent changes from plans, todo items, or intended work. Prefer user-facing impact over implementation detail.

## Format

Use grouped Markdown sections with concise bullets:

```markdown
# container-mcp <version>

## Features ✨

- Added ...

## Fixes 🐛

- Fixed ...

## CI / Release 👷

- Added ...

## Docs 📝

- Updated ...

## Tests 🧪

- Added ...
```

Allowed section names:

- `Features ✨`
- `Fixes 🐛`
- `Performance ⚡`
- `Security 🔒`
- `CI / Release 👷`
- `Build 📦`
- `Docs 📝`
- `Tests 🧪`
- `Refactors ♻️`
- `Breaking Changes ⚠️`

## Rules

- Start with `# container-mcp <version>`.
- Use only the section names listed above; do not translate them.
- Include only sections that have real entries. Never list empty categories.
- Do not list release assets unless the user explicitly asks for asset listings.
- Use past-tense or neutral bullets such as `Added`, `Fixed`, `Updated`, or `Clarified`.
- Do not include commit hashes unless the user asks.
- Do not include raw `git diff` output.
- Do not claim verification results unless they were actually run for the release.
- Keep bullets specific and short.
