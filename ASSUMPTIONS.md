# Assumptions

Decisions made where the brief does not specify behaviour. Each entry states the choice and why.

## Identity and input

1. **Student id format.** `X-Student-Id` must be exactly 17 ASCII digits (the birth registration
   number). Anything else is rejected with `400`. Surrounding whitespace is trimmed first because
   it is a common copy-paste artefact. *Why:* the brief fixes the length at 17 digits; validating
   strictly prevents the same student appearing under two slightly different ids.
2. **`GET /holds/{holdId}` does not require the header.** The brief lists the header only for
   endpoints 4, 6, 7 and 8. The detail endpoint therefore works without it, so a parent can
   paste a hold link anywhere. The response includes `studentId` as a permitted extra field.
3. **Class names need not be unique.** Nothing in the brief says they must be, and a school may
   legitimately reopen a class with the same name in a new year.
4. **Seat limit.** `totalSeats` must be between 1 and 100 000. Zero-seat classes are meaningless
   and an upper bound protects against typos such as `1200000`.

## Ids

5. **Id format.** Classes and holds use UUIDs rendered as plain strings. The brief says the format
   is free; UUIDs are collision-free across multiple API instances without coordination, and a
   non-UUID in the URL is cleanly rejected with `404` by routing.

## Time and expiry

6. **Source of "now".** The API process clock in UTC, through one `ISystemClock` abstraction.
   All containers in the compose stack share the host clock, so two API instances agree.
   The abstraction also lets tests advance time instead of sleeping.
7. **Expiry is computed, not scheduled.** A hold is treated as `EXPIRED` the instant
   `expiresAt <= now`, in every read and every write. No background job is needed for a seat to
   become available again (rule 2). A housekeeping sweeper does run every 30 s to persist the
   `EXPIRED` status so the stored data matches what the API reports, but correctness never depends
   on it. Interval is configurable with `HOLD_SWEEP_INTERVAL_SECONDS`.
8. **`HOLD_DURATION_SECONDS` validation.** Missing means 600. A non-positive or non-numeric value
   stops the process at startup with a clear message rather than silently falling back, because a
   wrong expiry in production would be worse than a loud failure.

## Concurrency (rule 1 and the two-process bonus)

9. **Row lock on the class.** Every seat allocation runs in one PostgreSQL transaction that begins
   with `SELECT ... FROM classes WHERE id = $1 FOR UPDATE`. PostgreSQL serialises all other
   allocations for that class behind it, so "count active, compare with totalSeats, insert" can
   never interleave. Because the lock lives in the database, it holds across any number of API
   processes sharing that database. Different classes lock different rows and proceed in parallel.
10. **Database-level safety nets.** A partial unique index on `(class_id, student_id) WHERE status
    IN ('HELD','CONFIRMED')` enforces rule 3 even if some future code path bypasses the lock. A
    check constraint enforces `total_seats > 0`. A unique-violation from the index is translated
    into `409`, never `500`.
11. **Migrations with several API instances.** Startup migration is wrapped in a PostgreSQL
    advisory lock so two instances starting together cannot both try to create the schema.

## Hold lifecycle

12. **Confirm.** Only the owner (`403` otherwise), only while `HELD` and unexpired (`409`
    otherwise). Confirming an already `CONFIRMED` hold returns `200` with the same body and
    changes nothing, so `confirmedSeats` is incremented exactly once (rule 5).
13. **Cancel (`DELETE`).** Allowed for `HELD` **and** `CONFIRMED` applications; both release the
    seat. *Why:* the brief shows a cancel button on every application and does not forbid
    withdrawing after paying; a parent who changes schools should be able to free the seat.
    Cancelling an already `CANCELLED` hold returns `204` again (idempotent). Cancelling an
    `EXPIRED` hold returns `409` because there is nothing left to cancel.
14. **Status codes used.** `400` bad input or missing header, `403` not the owner, `404` unknown
    class or hold, `409` no seats / duplicate application / wrong state, `201` on create, `204`
    on cancel. Every error body is RFC 7807 ProblemDetails with an extra machine-readable `code`
    field the UI maps to a human message.

## Configuration

15. **Database connection** comes from the standard ASP.NET Core variable
    `ConnectionStrings__Default`. The value in `appsettings.json` only points at a local
    development database and is overridden in `docker-compose.yml`.
