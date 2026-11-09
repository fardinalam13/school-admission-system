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
16. **Frontend backend URL is a runtime setting.** Vite normally bakes `VITE_*` variables in at
    build time, which would force an image rebuild to change the backend address. Instead the
    nginx container writes `/config.js` from the `API_BASE_URL` environment variable on every
    start and the app reads `window.__APP_CONFIG__`. `VITE_API_BASE_URL` still works for local
    `npm run dev`. *Why:* the brief asks for the URL to be changeable through an environment
    variable, and the evaluator should not need to rebuild to do so.
17. **The browser calls the backend directly** at `http://localhost:8080` (no reverse proxy in the
    frontend container). That keeps the API contract exactly as the brief specifies it and keeps
    CORS the backend's responsibility. Consequence: `API_BASE_URL` must be the address reachable
    from the user's browser, not the compose-internal hostname.

## Frontend behaviour

18. **Student id lives in localStorage**, keyed per browser. There is no login, so a refresh or
    a reopened browser restores the same student and re-fetches their applications (rule 11).
    The input strips non-digits as you type and applications are only enabled at exactly 17 digits.
19. **Seat counts poll every 3 seconds**, applications every 5 seconds, and polling pauses while
    the tab is hidden. *Why:* "every few seconds" in the brief; 3 s is responsive without hammering
    the API when thousands of parents have the page open. WebSockets/SSE were deliberately not
    used to keep the backend contract to the eight listed endpoints.
20. **Expiry is shown client-side first.** Each application card ticks once a second against its
    `expiresAt`; at zero it shows *Expired* and disables *Pay admission fee* without a reload or a
    server call (rule 9). The next poll then reflects the server's view. Clock skew between the
    browser and server could make the switch a second or two early or late; the server remains
    authoritative and any premature confirm attempt gets a readable `409`.
21. **Cancel asks for confirmation** in the browser because it is irreversible and releases the
    seat to others. Confirming (paying) does not, because a double click is harmless: the backend
    confirm is idempotent.
22. **Every error is shown as a sentence.** Backend error codes are mapped to friendly wording;
    unknown codes fall back to the backend's `detail` text; a failed network call shows a
    "cannot reach the admission server" message. Live-refresh failures keep the last good data on
    screen and say so, instead of blanking the page (rule 12).
23. **React 18.3 with plain JavaScript.** The brief allows JavaScript and React 18 or newer. The
    latest React 18 line was chosen over 19 to match the wording of the brief exactly and because
    the testing libraries used are long-stable on it.
