## Project planning source of truth

- Use `docs/roadmap.md` as the central place for project planning.
- When discussing phase status, next steps, or overall implementation progress, update `docs/roadmap.md` rather than creating a separate planning document.
- If code changes materially change project status, keep the roadmap statuses and notes in `docs/roadmap.md` in sync.

## Working conventions

- Prefer focused vertical slices that leave the project in a runnable, testable state.
- Keep protocol, server-core, rendering, and native-host responsibilities separated.
- When a roadmap phase is only partially complete, say so explicitly in `docs/roadmap.md`.
