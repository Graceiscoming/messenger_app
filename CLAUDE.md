# Messenger App (CP353 โจทย์ A) — Project rules for Claude and subagents

## Source of truth
The design in `docs/` is binding. Read the relevant file before writing code:
- Requirements & business rules: `docs/01-requirements.md` (BR-1..11 = section 4)
- Architecture / layering: `docs/03-architecture-c4.md`
- Entities, services, repositories, DTOs: `docs/04-class-diagram.md`
- Schema, constraints, indexes: `docs/05-database-erd.md`
- REST + SignalR contract: `docs/06-api-spec.md`
- Flows: `docs/07-sequence-diagrams.md`
- Structure, config, iterations, tests: `docs/08-implementation-guide.md`
- Agent team & workflow: `docs/09-agent-team.md`

If the design is wrong or incomplete, do NOT silently deviate: make the smallest sensible choice, and record it in
`docs/decisions.md` (date, what, why). Never change a REST path, DTO field or SignalR event name without recording it.

## Stack
- `src/Messenger.Api` (ASP.NET Core 8 Web API + SignalR) → `src/Messenger.Domain` ← `src/Messenger.Data` (EF Core 8 + Npgsql)
- Dependency direction is strict: **Domain references nothing** (no ASP.NET, no EF). Data references Domain. Api references both.
- `web/` React 18 + TypeScript + Vite, React Query, `@microsoft/signalr`
- PostgreSQL 16; everything runs with `docker compose up`
- Tests: `tests/Messenger.Domain.Tests` (xUnit + NSubstitute), `tests/Messenger.Api.Tests` (WebApplicationFactory + Testcontainers)

## Conventions
- C#: nullable enabled, file-scoped namespaces, async all the way, `CancellationToken` on public async APIs.
- Business rule violations throw domain exceptions (`NotFoundException`, `ForbiddenException`, `ConflictException`,
  `ValidationException`) → middleware maps to ProblemDetails 404/403/409/400. Controllers contain no business logic.
- DB naming: snake_case tables/columns (use `EFCore.NamingConventions`).
- JSON: camelCase; enums serialized as strings (`"Text"`, `"Group"`).
- Code/comments in English; user-facing UI text in Thai.
- No secrets in git: use env vars / `.env` (gitignored); commit `.env.example` only.

## Ownership (one writer per area — see docs/09-agent-team.md)
| Area | Owner agent |
| :--- | :--- |
| `src/Messenger.Domain/**`, `tests/Messenger.Domain.Tests/**` | backend-domain |
| `src/Messenger.Data/**` | backend-data |
| `src/Messenger.Api/**` | backend-api |
| `web/**` | frontend-web |
| `tests/Messenger.Api.Tests/**` | qa-tester |
| `docker-compose.yml`, `**/Dockerfile`, `web/nginx.conf`, `.env.example` | devops |

An agent may read anything but edits only its own area. Need a change elsewhere? Report it back to the orchestrator.

## AI usage log (course requirement)
Every agent appends one line per task to `docs/ai-usage-log.md`:
`| date | agent | files/area | what AI produced | what a human must check |`
Be honest about anything unverified (not built, not run, guessed API).

## Definition of done for any task
1. Builds (`dotnet build Messenger.sln` / `npm run build` in `web/`) with no new warnings-as-errors.
2. Relevant tests pass (`dotnet test`, or state clearly that the toolchain was unavailable).
3. Matches docs contract; deviations recorded in `docs/decisions.md`.
4. AI usage log line added.
