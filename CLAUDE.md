# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project context

ExpenseHub is a FIAP C# checkpoint (group assignment, due 2026-10-13): a corporate expense-reimbursement REST API built with ASP.NET Core (.NET 10), ASP.NET Core Identity, EF Core on a relational provider, bearer auth, and role-based authorization. The repo starts as a skeleton (only `GET /health` in `sources/ExpenseHub.Api/Program.cs`); everything else is implemented incrementally, one backlog issue per branch.

The spec lives in `docs/` (in Portuguese) and is the contract. Read it before implementing a feature:
- `docs/REQUISITOS.md`: roles, entities, field validations, state machine, required endpoints, HTTP status contract.
- `docs/MATRIZ-AUTORIZACAO.md`: authorization matrix and mandatory negative cases.
- `docs/RUBRICA.md`: grading. `docs/code-quality-rules.md`: CI quality scoring.
- `docs/PROCESSO-GITHUB.md`: branch/PR/commit workflow.

## Commands

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj

# single test / class (MSTest)
dotnet test ./sources/ExpenseHub.slnx --filter "FullyQualifiedName~ClassName.MethodName"

# local run of the CI quality pipeline (writes artifacts/code-quality/)
pwsh ./scripts/Invoke-CodeQuality.ps1            # add -SkipGitleaks if gitleaks isn't installed
```

## Build & code-quality constraints

- `Directory.Build.props` applies to all projects: `Nullable` enabled, **`ImplicitUsings` disabled** (every file needs explicit `using` directives), `GenerateDocumentationFile`, `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild`, StyleCop.Analyzers.
- Warnings are not errors, but full Code Quality score requires **zero warnings**. The CI script (`.github/workflows/build.yml` -> `scripts/Invoke-CodeQuality.ps1`) deducts per distinct `IDE*`/`CA*` ID and per FIAP rule. Notable FIAP rules to avoid: tracked `bin/obj/.vs`/binaries, secrets or connection strings with credentials in versioned config, plaintext passwords, `CountAsync() > 0` (use `AnyAsync`), `Thread.Sleep`, synchronous DB queries in controllers, entities bound directly as API input (use DTOs), DTOs without data-annotation validation, leftover `WeatherForecast` scaffolding, failing tests.
- `.editorconfig` style highlights: file-scoped namespaces, braces always required, no `this.` qualification, `_camelCase` private fields, explicit accessibility modifiers, usings outside namespace with `System` first, explicit types for built-ins (`var` elsewhere). `**/Migrations/*.cs` is treated as generated code.
- Unit tests (MSTest 4, `ExpenseHub.UnitTests`, method-level parallelization) must run without a database, network, or external services. Only unit tests count toward the grade.

## Architecture rules from the spec

- Roles: `Employee`, `Approver`, `Finance`, `Auditor`, `Admin`. Roles accumulate (union of permissions), but nobody may approve or pay their own expense, and `Admin` grants no functional expense access. Seed creates only the roles plus a single Admin user; `/register` must never accept roles; after a role change the user must re-authenticate.
- Entities: `Expense` (single amount, `decimal`, no item collection), `ExpenseCategory`, `ExpenseHistory`, `PaymentRecord`.
- State machine: `Draft` -> (submit, owner) `Submitted` -> (approve/reject, Approver non-owner) `Approved`/`Rejected` -> (pay, Finance non-owner) `Paid`. `Rejected`/`Paid` are final. Repeating/invalid transitions return `409` and must not write duplicate history. No generic endpoint may change state.
- Role attributes on endpoints must be combined with ownership + state checks in the **service layer**. Owner, state, actor, and timestamps always come from the token/server, never the client.
- Visibility filters (Employee: own; Approver: `Submitted`; Finance: `Approved`/`Paid`; Auditor: all) must be applied in the query before materialization, never load-then-filter. Out-of-scope resources return `404`.
- Every change and its `ExpenseHistory` row (action, actor, UTC time, previous/next state, rejection reason, Draft edits) must be persisted in the same logical operation.
- Errors use `ProblemDetails` with statuses 400/401/403/404/409 as defined in `REQUISITOS.md`.
- Database provider is the group's choice; document provider, package, config, and migration steps in README. Don't commit credentials.

## Git workflow

- One branch per backlog issue, named like `i06-ownership`; PR title like `I06: Ownership e matriz de acesso`.
- PR description references the central issue as `Racass/checkpoint-csharpracass-expensehub#N` and must **not** use `Closes`/`Fixes`/`Resolves`.
- Conventional commit messages (e.g. `feat(expenses): ...`, `test(expenses): ...`, `docs: ...`). Commits are used to assess each member's participation.

## Working rules (added by the team)

- Work on one backlog issue at a time. Implement only what the current issue asks for. Do not start later issues.
- Read the issue's acceptance criteria and negative cases first. Propose a plan and wait for approval before editing.
- Before saying a task is done: `dotnet build` must show 0 errors and 0 warnings, and `dotnet test` must pass.
- Fix the cause of warnings. Never use `#pragma warning disable`, `[SuppressMessage]`, `NoWarn` or severity changes to hide them.
- Never edit `.editorconfig`, `Directory.Build.props`, `.github/`, `scripts/` or anything under `docs/`.
- Never run `git commit` or `git push`. Leave changes uncommitted. The developer reviews the diff and commits.
- No secrets in code, config, logs or prompts. The Admin seed password must come from configuration (user-secrets or an environment variable), never from a versioned file.
- Do not commit `bin/`, `obj/`, `.vs/`, database files or `artifacts/`. Check `git status` before finishing.
- No em dashes in README, PR text, commit messages or comments.
- Code identifiers in English. README and PR text in Portuguese.
- Use async/await for all I/O and never `.Result` or `.Wait()`.
- Ask before adding any NuGet package that is not part of the approved plan.
- Every issue that adds a business rule must include MSTest unit tests for valid and invalid cases. Test names state the rule being checked.
- Keep business rules (state transitions, ownership, validation) in plain classes that do not need a `DbContext`, so they can be unit-tested without a database.

## Provisional decisions (until the professor answers; keep status-code logic in one place so it is easy to change)

- `ExpenseCategory`: keep it minimal (Id, Name) and do not link it to `Expense` yet.
- Check order for expense operations: 401, then 400 (DTO validation), then 404 (missing or outside the caller's read scope), then 403 (role or ownership forbids the action), then 409 (wrong state or repeated transition).
- Approver or Finance acting on their own expense: 403. Employee touching another Employee's expense: 404.
- Unit tests must not touch any database, including EF InMemory or SQLite in-memory, until the professor confirms it is allowed.