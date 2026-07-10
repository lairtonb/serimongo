# Agent Instructions

These instructions apply to the whole repository.

## Commit Discipline

- Commit every completed user-requested change before the final response.
- If the user explicitly says not to commit, do not commit.
- Keep unrelated work in separate commits whenever practical.
- Before committing code changes, inspect `git status`, `git diff`, and recent
  history, then stage only the intended files.
- If a change is reverted before commit, verify that the reverted files no
  longer appear in `git status`.
- Mention the commit hash in the final response.
