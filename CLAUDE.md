# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project context

ExpenseHub is a FIAP C# project: a corporate expense-reimbursement REST API built with ASP.NET Core (.NET 10), ASP.NET Core Identity, EF Core on SQLite, bearer auth, and role-based authorization. The CP2 (Checkpoint 2) delivery is closed: it is the merge commit `2e74619` on `main` (PR #10), and its grade is not recalculated.

The current work is **CP3 (Checkpoint 3), "ExpenseHub em operação"**, due **2026-11-04 at 23:59 on Teams** (the apostila and the central issues say "04/10" by mistake; Teams says November 4). Renan does CP3 alone, in this same repository. CP3 adds asynchronous expense reports: the web app registers a request and publishes a message to RabbitMQ, a separate C# worker consumes it, calculates and persists the result, the user tracks progress and downloads an Excel (.xlsx) file. Everything must run from two published Docker images (web and worker) with Docker Compose.

Where the specs live:
- `andamento/PROGRESSO.md`: temporary working file with the distilled CP3 contract, the technical watch points, the status table and one section per issue. Read it fully before every issue and update it when you finish.
- `andamento/requisitos/`: the CP3 apostila and slides (HTML, the authority when in doubt) and `issues-cp3.md`, the text of the central issues #11 to #18 exactly as the professor published them (each one repeats its part of the contract). Do not dump whole files: convert the HTML to text outside the repository (for example with `sed` into a scratch file) and read only the part about the issue in hand.
- `docs/` (Portuguese) is the CP2 spec and still binds the existing behavior. Read it when a change touches existing features:
  - `docs/REQUISITOS.md`: roles, entities, field validations, state machine, endpoints, HTTP status contract.
  - `docs/MATRIZ-AUTORIZACAO.md`: authorization matrix and mandatory negative cases.
  - `docs/RUBRICA.md`: CP2 grading. `docs/code-quality-rules.md`: CI quality scoring.
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

The commands for the worker, RabbitMQ and Docker Compose are added to the README by CP3-I03 and CP3-I05.

## Build & code-quality constraints

- `Directory.Build.props` applies to all projects, including the new ones: `Nullable` enabled, **`ImplicitUsings` disabled** (every file needs explicit `using` directives), `GenerateDocumentationFile`, `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild`, StyleCop.Analyzers.
- Warnings are not errors, but full Code Quality score requires **zero warnings**. The CI script (`.github/workflows/build.yml` -> `scripts/Invoke-CodeQuality.ps1`) deducts per distinct `IDE*`/`CA*` ID and per FIAP rule. Notable FIAP rules to avoid: tracked `bin/obj/.vs`/binaries, secrets or connection strings with credentials in versioned config, plaintext passwords, `CountAsync() > 0` (use `AnyAsync`), `Thread.Sleep`, synchronous DB queries in controllers, entities bound directly as API input (use DTOs), DTOs without data-annotation validation, leftover `WeatherForecast` scaffolding, failing tests.
- `.editorconfig` style highlights: file-scoped namespaces, braces always required, no `this.` qualification, `_camelCase` private fields, explicit accessibility modifiers, usings outside namespace with `System` first, explicit types for built-ins (`var` elsewhere). `**/Migrations/*.cs` is treated as generated code.
- Unit tests (MSTest 4, `ExpenseHub.UnitTests`, method-level parallelization) must run without a database, network, RabbitMQ or Docker. Only unit tests count toward the grade. Never use EF Core InMemory or SQLite in-memory in unit tests (professor's guidance): data access sits behind repository interfaces and unit tests use fakes or mocks of them. The database is checked by the manual evidence documented in the README; the repository has no functional tests. The Excel file is built in memory and re-opened with the same library in tests; the broker is replaced by a hand-written fake publisher.
- CP3 is graded by objective rubric units (see `andamento/PROGRESSO.md`). A unit only counts when behavior and evidence match the requirement. Quantity of tests and coverage percentage give no points.

## Architecture rules from the CP2 spec (still valid)

- Roles: `Employee`, `Approver`, `Finance`, `Auditor`, `Admin`. Roles accumulate (union of permissions), but nobody may approve or pay their own expense, and `Admin` grants no functional expense access. Seed creates only the roles plus a single Admin user; `/register` must never accept roles; after a role change the user must re-authenticate.
- Entities: `Expense` (single amount, `decimal`, no item collection), `ExpenseCategory`, `ExpenseHistory`, `PaymentRecord`.
- State machine: `Draft` -> (submit, owner) `Submitted` -> (approve/reject, Approver non-owner) `Approved`/`Rejected` -> (pay, Finance non-owner) `Paid`. `Rejected`/`Paid` are final. Repeating/invalid transitions return `409` and must not write duplicate history. No generic endpoint may change state.
- Role attributes on endpoints must be combined with ownership + state checks in the **service layer**. Owner, state, actor, and timestamps always come from the token/server, never the client.
- Visibility filters (Employee: own; Approver: `Submitted`; Finance: `Approved`/`Paid`; Auditor: all) must be applied in the query before materialization, never load-then-filter. Out-of-scope resources return `404` on reads (list, detail and history). The actions that write do not use the read scope (see Decisions).
- Every change and its `ExpenseHistory` row (action, actor, UTC time, previous/next state, rejection reason, Draft edits) must be persisted in the same logical operation.
- Errors use `ProblemDetails` with statuses 400/401/403/404/409 as defined in `REQUISITOS.md`.
- Database provider is SQLite; document provider, package, config, and migration steps in README. Don't commit credentials.
- The CP3 work must not change this behavior, especially authentication, authorization and payment.

## CP3 rules from the apostila

- Routes: `POST /api/reports` (body `fromDate`, `toDate`, `scope` = `Mine` or `Global`; answers `202` with a `Location` header and the envelope), `GET /api/reports` (only the caller's own requests), `GET /api/reports/{id}` (own request, progress and result), `GET /api/reports/{id}/excel` (own `Completed` request, real `.xlsx`).
- Envelope: `id`, `scope`, `fromDate`, `toDate`, `status` (`Pending`, `Processing`, `Completed`, `Failed`), `createdAt`, `startedAt`, `completedAt`, `result`, `error`. `error` has a stable `code` and a sanitized `message`. Instants are UTC.
- Result: `generatedAt`, `totalCount`, `totalAmount`, `byStatus` with all five expense states (`Draft`, `Submitted`, `Approved`, `Rejected`, `Paid`), each with `count` and `amount`, zeros when empty. Decimals only (sum in integer cents, convert on output). The period filters `ExpenseDate` with both limits inclusive, never the creation date. Status and amount are read at processing time. The result is saved and never recalculated on read.
- Authorization: `Employee` may request `Mine`; `Auditor` may request `Global`; with both roles, both. `Admin`, `Approver` and `Finance` without those roles get nothing. List, detail and Excel return only the caller's own requests, even for `Auditor`. The requester and the scope come from the server (token and persisted record), never from the body or from the message. For `Mine`, the worker filters by the registered requester.
- Status codes: `401` anonymous; `403` role cannot request that scope; `400` invalid dates or scope; `404` detail or Excel of a request that does not exist or belongs to someone else (also for `Auditor`); `409` Excel of an own request that is not `Completed` (pending, processing or `Failed`); `202` only after the request is persisted and the broker confirmed the publish; `503` with the persisted id and a sanitized error when the publish is not confirmed; an explicit error, without `202` and without a made-up id, when the database is down before registering.
- Messaging: durable queue, persistent messages, publisher confirms, message contract `{"reportId": "..."}`. The worker loads period, scope and requester from the persisted record. The consumer acknowledges only after the result (or the failure) is saved. Idempotency is persistent (it survives a worker restart): a `reportId` already `Completed` produces no new result or effect. `Completed` and `result` are saved together. A definitive failure becomes `Failed` with a sanitized error and is not requeued forever. A transient failure is never acknowledged as if it had been saved. A `Processing` request must be resumable. There is no Transactional Outbox; document the window between saving and publishing (README and dossier).
- Excel: a real `.xlsx` (not CSV), `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `Content-Disposition: attachment` with `report-{id}.xlsx`. It carries id, scope, period, generation instant and a table with the five states, counts, amounts and the grand total, in numeric cells. It is built from the persisted result, without recalculating and without another message. Pick a library with a permissive license and write it in the README.
- Delivery: two real images (web and worker) published only after the tests pass, tagged with the full commit SHA (never `latest`); Docker Compose runs those images plus RabbitMQ with persistent configuration; web and worker share the same database state; instructions and example configuration without secrets.
- Logs are correlated by `reportId` (publish, start, finish, duplicate, failure) and never contain tokens, passwords, authorization headers or connection strings.
- Out of scope: new frontend, assistant, new auth, asynchronous payment, cloud, Kubernetes, mandatory scaling, general database migration. Outbox, full retry policy and dead-letter queue are optional and give no bonus.

## CP3 working protocol (one plan, then execute)

The goal is few messages and no rework. Follow these steps for every issue.

1. **Start.** Read `andamento/PROGRESSO.md` fully and the apostila part for the issue. Check `git branch --show-current` (it must be the issue branch named in the PROGRESSO table; if not, stop and tell the developer) and `git status` (clean, except files this issue will create).
2. **Plan once.** In plan mode, produce a single plan that lists: the rubric units of the issue and how each one will be proven; files to create or change; new packages (name, version, license); the unit tests by name and the rule each one checks; the commands you will run; risks and open questions; documentation to update; and the suggested commit groups. Do not edit anything before the plan is approved.
3. **Execute everything.** After approval, carry out the whole plan without asking for confirmation again. Run `dotnet build` and `dotnet test` yourself and fix failures yourself. Run the quality script after `git add -A` (staging only, to feed the script; never commit) and read the finding IDs, not only the score.
4. **Stop and ask only when:** a package is needed that the plan did not approve; a protected file would have to change; the spec and the CP2 behavior conflict; a decision cannot be undone and could reasonably go either way; the build or tests still fail after three honest attempts; something needs a secret; or you find a CP2 defect that blocks the issue (describe it; fix it only if the issue cannot work without it, and say so).
5. **Finish with a report** (format below). Do not start the next issue.

### Final report format (in Portuguese, no em dashes)

1. Summary in three lines.
2. Rubric units: for each unit, `met` or `not met`, and where it is proven (file, test or evidence).
3. Files created and changed (`git status --short` and `git diff --stat`).
4. Tests added (names) and results: build warnings and errors, `dotnet test` totals.
5. Quality script: score and finding IDs.
6. Decisions taken and deviations from the plan.
7. Risks and pending items.
8. Suggested commits: ready-to-run commands, one block per commit (`git add <paths>` then `git commit -m "..."`), English messages, conventional style, issue id in parentheses at the end of the first line. Because the index may be fully staged by step 3, start with `git reset` and say so.
9. Pull request text: write it to `andamento/PR-CP3-INN.md` (Portuguese, with the checklist from `docs/PROCESSO-GITHUB.md` unticked) and only say the file name here.
10. What you updated in `andamento/PROGRESSO.md`, `CHECKPOINT-3.md` and `README.md`.

## Organization of the documentation

- `README.md` is the landing page: what the project is, a team table with one column per checkpoint (Renan: CP2 and CP3; Pedro: CP2 only), how to run the current version, endpoints, tests, a short section per checkpoint with a link to its record, and the requirement table of each issue with its status and PR.
- `CHECKPOINT-2.md` is the frozen record of everything done in CP2 (Pedro's name stays there). Only fix links in it.
- `CHECKPOINT-3.md` is the CP3 record, filled issue by issue like CP2 was: per-issue detail, decisions, evidence, and the E01 to E05 dossier. It names only Renan.
- At the end of each issue update: the status table in the README, the section of the issue in `CHECKPOINT-3.md`, and `andamento/PROGRESSO.md`.

## Git workflow

- One branch per CP3 issue, named as the issues suggest: `cp3-i01` to `cp3-i07`, plus `cp3-00-preparacao` for the preparation. The developer creates and switches branches.
- PR title like `CP3-I01: Contrato e resultado do relatório`. The PR description references the central issue as `Racass/checkpoint-csharpracass-expensehub#11` (I01 = #11, I02 = #12, I03 = #13, I04 = #14, I05 = #15, I06 = #16, I07 = #17) and must **not** use `Closes`/`Fixes`/`Resolves`. The central issues stay open.
- Conventional commit messages in English, with the issue id in parentheses at the end of the first line, for example `feat(reports): add report request endpoint (CP3-I01)`. Do not write `#N` in commit text, because GitHub links it to an item of the team repository.
- The developer merges the PR with "Create a merge commit" (never squash or rebase) and keeps the branches after the merge. The professor reads the commits.
- Commits are made by the developer after reviewing the diff.

## Working rules

- Work on one issue at a time. Implement only what the current issue asks for. Do not start later issues.
- Before saying a task is done: `dotnet build` must show 0 errors and 0 warnings, `dotnet test` must pass, and the quality script must have run.
- Fix the cause of warnings. Never use `#pragma warning disable`, `[SuppressMessage]`, `NoWarn` or severity changes to hide them.
- Never edit `.editorconfig`, `Directory.Build.props`, `scripts/` or anything under `docs/`. Never edit `.github/workflows/build.yml` (the professor's pipeline). The only change allowed under `.github/` is creating a NEW workflow file to publish the two images in CP3-I05, and only if the approved plan says so.
- Never run `git commit`, `git push`, `git merge`, `git rebase`, `git reset`, `git checkout`, `git restore`, `git stash` or `git clean`. The only git command that changes state and is allowed is `git add`, to stage files for the quality script. Leave the commits to the developer.
- No secrets in code, config, logs, prompts or example files. The Admin seed password and the RabbitMQ credentials come from configuration (environment variables or user-secrets), never from a versioned file. Provide `.env.example` with placeholders that contain no real values.
- Do not commit `bin/`, `obj/`, `.vs/`, database files, `artifacts/`, `.env`, Data Protection keys, RabbitMQ data or downloaded spreadsheets. Check `git status` before finishing.
- No em dashes in any text you write: README, markdown files, PR text, commit messages, comments, log messages and test names.
- Code identifiers in English. README, `CHECKPOINT-*.md` and PR text in Portuguese. Commit messages and XML documentation comments in English.
- Use async/await for all I/O and never `.Result` or `.Wait()`.
- New NuGet packages: only the ones the approved plan names (the RabbitMQ client in CP3-I03 and an `.xlsx` library in CP3-I01, each with version and license stated in the plan). Ask before any other package, and ask before any mocking package.
- Every issue that adds behavior includes MSTest unit tests for valid and invalid cases. Test names state the rule being checked. Write the tests while developing the issue, not at the end.
- Services depend on repository interfaces (for example `IExpenseRepository`) implemented with EF Core and registered in DI. Services never take `DbContext` directly, so unit tests can replace the repositories with fakes. The same applies to the report publisher (for example `IReportPublisher`): the broker sits behind an interface.
- Keep business rules (period validation, scope by role, totals, state transitions, ownership) in plain classes or service methods that can be tested without EF Core, RabbitMQ or HTTP. Prefer hand-written fakes.
- `ExpenseService` is not sealed and its public methods are `virtual`, so the controller tests can replace it with the hand-written `FakeExpenseService`. Do not seal it again.
- Mark an issue as "Concluída" in the README status table only when every acceptance criterion is met, and fill in the PR number once the PR exists.
- Money is stored as integer cents (`MoneyConversion`) and `ToCents` rounds. DTOs must reject amounts with more than two decimal places. Never rely on the converter to round user input. Never use `double` or `float` for money.
- Every new persisted `DateTimeOffset` needs the `UtcDateTimeOffsetConverter` (UTC ticks), otherwise `OrderBy`, `Max` and `Where` fail on SQLite.
- Never assign a literal to a field, variable, property or JSON key whose name contains password, passwd, pwd, senha, secret, token or api key, not even in unit tests, `.http` files or example Compose files: the quality script flags it as FIAP1002 in tracked files, which caps the score at 9. Build test credentials at runtime (for example `new string('a', 12)`) or read them from configuration.
- Docker: images are tagged with the full commit SHA, never `latest`. Never run `docker compose down -v`, `docker volume rm`, `docker volume prune` or `docker system prune`, and never delete the database or a volume to make something pass. The developer works on Windows with Git Bash: put `MSYS_NO_PATHCONV=1` before `docker` commands that receive Linux paths, so Git Bash does not rewrite them.
- Keep `andamento/PROGRESSO.md` current. That folder is temporary and is removed in CP3-I07, together with this paragraph and the references to `andamento/` in this file.

## Decisions (keep status-code logic in one place; document new CP3 decisions in CHECKPOINT-3.md; the CP2 reasoning lives in CHECKPOINT-2.md)

- `ExpenseCategory` (professor confirmed no issue requires it): map only the minimal entity (Id, Name). No endpoints, no seed and no link to `Expense`.
- The professor asked the team to choose status codes by their meaning: 401 is about authentication (who am I), 403 about authorization (what may I do), 404 not found, 409 conflict.
- Order for the actions that write (edit, submit, approve, reject and pay), decided by the professor: 401 (not authenticated), then 403 from the role attribute (authenticated but without the role), then 400 (DTO validation). Inside the service: 404 only when the expense does not exist, then 403 (ownership rule forbids the action), then 409 (the state does not accept the transition or it was repeated, including a `Draft`, `Approved`, `Rejected` or `Paid` expense that gets the wrong action). These actions load the expense by identifier alone and never use the read scope.
- Reads (list, detail and history) keep the read scope: an expense outside it returns `404`, the same answer as one that does not exist.
- Employee sending or editing another Employee's expense: 403, because the expense exists but is not theirs ("o erro é de auth/authz, não de not found"). Approver or Finance acting on their own expense: 403.
- Paying goes through the same single path as approving and rejecting, takes no body, and only moves `Approved` to `Paid`. The `PaymentRecord` (actor from the token, time from the server), the new `Status` and the `Paid` history row are prepared before one `SaveChangesAsync`, so they are written together or not at all. A persistence failure that is not a conflict propagates (`500`) and is never swallowed.
- Simultaneous payments of the same expense have two barriers. The `Status` concurrency token stops the real race (the `UPDATE` carries `WHERE Status = <value read>`), and the unique index on `PaymentRecords.ExpenseId` is an extra defense. The repository turns both failures (`DbUpdateConcurrencyException` and a SQLite unique violation, codes 19 and 2067) into `ExpenseConflictException`, which the service answers with `409`. Other constraint failures (foreign key, `CHECK`, trigger, busy database) are not conflicts and are not converted.
- The history follows the visibility of the expense, as the professor confirmed on Teams ("quem vai ler elas vai ser o auditor"): `404` outside the read scope, so an Approver stops reading the history of an expense after deciding it, and Finance reads only `Approved` and `Paid`. Entries come oldest first (`OccurredAtUtc`, then `Id`) and carry only internal identifiers, never an e-mail address.
