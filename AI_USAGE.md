# AI Usage

Running log of where AI tooling was used, what it got wrong, and how the mistakes were caught.

## Tool

Claude Code (Anthropic), used inside VS Code.

## Log

- 2026-11-09 — Used Claude to analyse the assignment brief, list the mandatory rules, identify
  ambiguities to document in ASSUMPTIONS.md, and produce the phased implementation plan.
  Used it to check which tools were already installed and to scaffold the repository.
- 2026-11-09 — Backend (Phase 1). Used Claude to scaffold the ASP.NET Core project, design the
  data model, write the EF Core configuration and migration, and implement the service layer and
  controllers. The concurrency approach (PostgreSQL `SELECT ... FOR UPDATE` on the class row
  inside one transaction, with expiry computed from `expires_at` instead of a background job)
  was discussed and chosen before any code was written.
  - *Mistake caught:* the first scaffold added `EnableRetryOnFailure()` to the Npgsql options.
    That execution strategy is incompatible with the explicit `BeginTransaction()` the hold
    logic relies on (EF throws at runtime), so it was removed before the hold code was written.
  - *Mistake caught:* an initial version of the partial unique index on
    `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` would have blocked a student
    from re-applying after their previous hold expired, because the stale row is still stored as
    `HELD` until something updates it. Fixed by persisting `EXPIRED` for stale rows of the class
    inside the locked allocation transaction before checking for duplicates.
  - *Mistake caught:* a shell script generated to write several source files at once had a
    quoting error and wrote nothing; the files were written individually instead.
  - *Mistake caught:* after `dotnet ef migrations add --no-build`, the API was launched with
    `dotnet run --no-build`, so it ran the previous binaries and reported "schema up to date"
    while creating no tables. Caught by inspecting the database directly; fixed by rebuilding.
  - *Mistake caught:* the seat-summary query projected into a record constructor and then applied
    `OrderBy`/`SingleOrDefault`, which EF Core cannot translate (500 on `GET /classes`). Caught by
    the curl smoke test; fixed by filtering/ordering before the projection.
  - *Mistake caught:* after the fix above, one smoke-test run still showed `heldSeats` and
    `confirmedSeats` as 0 while active holds existed. A clean rebuild and a re-run with EF SQL
    logging enabled produced correct counts and correct SQL, so the earlier run was most likely
    executed against stale binaries. Because "could not reproduce" is not proof, the Phase 3
    integration tests include an explicit seat-count assertion to guard this permanently.
  - *Mistake caught:* a diagnostic command set an environment variable whose name contains dots
    using bash `NAME=value cmd` syntax, which bash rejects, so the API silently never started and
    every request returned HTTP 000. Fixed by using `env 'NAME=value' cmd`.
  - *Mistake caught:* the first backend Dockerfile added a `HEALTHCHECK` using `curl`, but the
    official slim `aspnet:8.0` image does not ship `curl`, so the container would have been
    reported unhealthy forever. Removed before the image was ever built.
  - *Mistake caught:* the test project build warned about two different EF Core assembly versions
    (8.0.11 via the Npgsql provider, 8.0.33 via the Design package). Fixed by pinning
    `Microsoft.EntityFrameworkCore` and `.Relational` explicitly in the API project.
- 2026-11-09 — Tests (Phase 3). Claude wrote the xUnit integration suite (Testcontainers
  PostgreSQL, in-process `WebApplicationFactory`, fake clock) including the required concurrency
  test and a two-instance test for the bonus. I reviewed each assertion against the numbered rules
  in the brief before running them.
- 2026-11-09 — Frontend (Phase 4). I have no prior React/Vite experience, so Claude wrote the
  React components, hooks and Vitest tests and explained each piece. Decisions I made and asked
  for: plain JavaScript instead of TypeScript, React 18.3, polling instead of WebSockets, runtime
  `config.js` for the backend URL instead of a build-time variable, a browser confirm dialog only
  on cancel. The scaffold was hand-written rather than generated with `npm create vite` because
  that tool prompts interactively.
