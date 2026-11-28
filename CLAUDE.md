# D2PS2 work from this repository's cloud sessions

The Diablo II PS2 port (D2PS2) is developed on the user's PC **PowerStorm** (Desktop Commander device
`6b440ab5-a4ef-48aa-afce-2eb94c6b0ef8`), tree `D:\D2PS2\tree`. Its own `CLAUDE.md`, `AGENTS.md` and
`integration-logs/act1/HANDOFF.md` are the project's rules and resume point: read them first.

## Official project names (user, 2026-11-26; PS2 name changed 2026-11-28)
- PlayStation 2 port (D2PS2): **Diablo II: Obsidian Calling** (formerly "Obsidian Rune Edition")
- Original Xbox port: **Diablo II: Emerald Jewel Edition**
Use them in user-facing text; internal code names and paths stay.

## STANDING DIRECTIVE (user, 2026-11-26)

The primary objective is the **autonomous Act I completion directive**: a complete, faithful Act I
(the original PC game is the reference), researched, implemented, tested, audited and corrected until
Act I is genuinely complete.

- Any other user request is a temporary interruption: handle it, then return to Act I work automatically,
  unless the user explicitly changes or suspends the objective.
- On every resume: recover the state from the persistent records on PowerStorm, check the agents (Codebox,
  Cursor, local LLMs; Codex refuses D2 work), resume or reassign interrupted tasks, and start on the
  highest-priority gameplay deficiency.
- Distribute work to other agents and local LLMs to conserve premium usage (Codebox: light tasks only).
- Do not stop after one fix, build, test pass or milestone; when blocked, try another approach or another task.
- Limits: no destructive operations, no proprietary assets published, no new paid charges or permission
  changes without approval; the D2PS2 tree is committed locally and not pushed; never OneDrive; no keypresses
  on PowerStorm's desktop; never format memory cards; real-PS2 behaviour only from the user's reports.

<!-- BEGIN global autonomous development directive (user, 2026-11-26) -->
# Global Autonomous Development and Repository Management Directive

Applies to every project and session. Project-specific instructions (a project's own CLAUDE.md,
AGENTS.md, handoff docs) take precedence where they are stricter or more specific: for example a
project rule "commit locally, push nothing unless asked" or a harness rule "no pull request unless
asked" stays in force, and these global rules never replace a project's established objective.

## Repository management
- Manage commits, branches, checkpoints, pull requests and merges autonomously where authorized.
- Before changing anything, verify the project directory, repository, remote and branch. Never commit
  project files to an unrelated repository.
- Commit meaningful changes at verified milestones; keep recoverable checkpoints; verify repository integrity.
- Create, review and merge pull requests when appropriate and authorized; respect branch protections,
  permissions and the project's existing workflow. Routine repository management needs no manual step.
- Never force-push, destructively rewrite history or overwrite another agent's work without explicit
  authorization.
- Never commit credentials, proprietary assets, copyrighted game data or unrelated files (build outputs included).

## Autonomous development
- Keep each project's primary development objective. Intervening requests, troubleshooting and
  administrative tasks are temporary interruptions unless the user explicitly changes or suspends the
  objective; resume the objective automatically afterwards.
- Do not stop because a commit, pull request, build, test or milestone succeeded; continue while
  productive, authorized work remains.
- When blocked, try alternative approaches or give independent outstanding tasks to other agents.
- Ask the user only when genuinely necessary.

## Agents and resources
- Distribute work among Claude Code, Codex, DeepSeek, Cursor, subagents and local LLMs; delegate suitable
  tasks to cheaper models to conserve premium allowances; keep local LLMs busy when useful work exists.
- Coordinate concurrent agents (file ownership, no duplicated work); respect hardware limits, memory,
  service usage limits and stability.
- Verify delegated work with independent audits and automated tests.

## Persistence and recovery
- Keep per-project persistent records: objective, progress, outstanding tasks, known defects, recovery steps.
- Read them when a session starts or resumes; resume interrupted work after crashes, reboots, context
  resets or service interruptions.
<!-- END global autonomous development directive -->
