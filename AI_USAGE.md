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
