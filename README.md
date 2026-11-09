# School Admission System

Online admission for a school where every class shift has a limited number of seats, granted
first-come, first-served. Applying places a time-limited **hold** on a seat; paying the admission
fee within that time **confirms** it; otherwise the seat is released automatically.

| Part | Stack | Folder |
|---|---|---|
| Backend API | C# / ASP.NET Core 8, EF Core 8, PostgreSQL 16 | [`backend/`](backend/) |
| Frontend | React 18 (JavaScript), Vite, plain CSS | [`frontend/`](frontend/) |
| Orchestration | Docker Compose (PostgreSQL + backend + frontend) | [`docker-compose.yml`](docker-compose.yml) |

Design decisions that the brief left open are documented in [ASSUMPTIONS.md](ASSUMPTIONS.md).
How AI tooling was used, and where it went wrong, is in [AI_USAGE.md](AI_USAGE.md).

## Run with Docker

Prerequisite: Docker Desktop (or Docker Engine with the Compose plugin). Nothing else.

```bash
docker compose up --build
```

First start downloads base images and builds both apps; allow a few minutes. Then open:

| What | URL |
|---|---|
| Frontend (UI) | **http://localhost:3000** |
| Backend API | http://localhost:8080 |
| API health check | http://localhost:8080/health |
| Swagger UI | http://localhost:8080/swagger |

Stop with `Ctrl+C`, or `docker compose down`. Data lives in a named Docker volume and survives
`docker compose down` followed by `docker compose up`. To wipe everything: `docker compose down -v`.

### Configuration (environment variables)

| Variable | Used by | Default | Meaning |
|---|---|---|---|
| `ConnectionStrings__Default` | backend | compose-internal PostgreSQL | Npgsql connection string |
| `HOLD_DURATION_SECONDS` | backend | `600` | How long a seat stays held before it is released |
| `HOLD_SWEEP_INTERVAL_SECONDS` | backend | `30` | Housekeeping interval that persists `EXPIRED` status (correctness does not depend on it) |
| `ASPNETCORE_URLS` | backend | `http://+:8080` | Listen address |
| `API_BASE_URL` | frontend | `http://localhost:8080` | Backend address the browser should call |

Both `HOLD_DURATION_SECONDS` and `API_BASE_URL` can be overridden on the compose command line,
which is handy for watching holds expire quickly:

```bash
HOLD_DURATION_SECONDS=60 docker compose up --build
```

On Windows PowerShell: `$env:HOLD_DURATION_SECONDS=60; docker compose up --build`.

### Using the UI

1. Type the student's **17-digit birth registration number** in the box at the top. It is remembered
   in the browser, so a refresh or a reopened browser still shows that student's applications.
2. **Admin: create class** tab: enter a name (one per shift, e.g. "Class 6 - Morning Shift") and
   the number of seats.
3. **Classes** tab: live seat counts (refreshed every 3 seconds). Press **Apply** to hold a seat.
4. **My applications** tab: each application shows its status, a countdown, **Pay admission fee**
   (pretend payment, confirms the seat) and **Cancel**. When the countdown reaches zero the card
   turns to *Expired* and the pay button is disabled without any reload.

## API

Base URL `http://localhost:8080`, JSON in and out. The student is identified by the
`X-Student-Id` header (17 digits).

| # | Method and path | Purpose | Header |
|---|---|---|---|
| 1 | `POST /classes` | Create a class `{ "name", "totalSeats" }` → `201` | |
| 2 | `GET /classes` | All classes with `totalSeats`, `heldSeats`, `confirmedSeats`, `availableSeats` | |
| 3 | `GET /classes/{classId}` | One class with the same seat accounting | |
| 4 | `POST /classes/{classId}/holds` | Apply: hold a seat → `201`, or `409` when full / already applied | `X-Student-Id` |
| 5 | `GET /holds/{holdId}` | Current state: `HELD`, `CONFIRMED`, `EXPIRED` or `CANCELLED` | |
| 6 | `POST /holds/{holdId}/confirm` | Pay the fee; idempotent → `200 { holdId, status: "CONFIRMED" }` | `X-Student-Id` |
| 7 | `DELETE /holds/{holdId}` | Cancel the application → `204` | `X-Student-Id` |
| 8 | `GET /me/holds` | All applications of this student, with `className` | `X-Student-Id` |

Errors are RFC 7807 `application/problem+json` bodies with an additional machine-readable `code`
field, for example:

```json
{ "title": "Conflict", "status": 409, "detail": "No seats are available in this class right now.", "code": "NO_SEATS_AVAILABLE" }
```

Invalid input is always a `4xx` (`400` bad body or missing/invalid header, `403` not the owner,
`404` unknown id, `409` business-rule conflict), never a `500`.

### How the seat limit is enforced under concurrency

Every seat allocation runs in one PostgreSQL transaction that starts by locking the class row with
`SELECT ... FOR UPDATE`. All other applications for the same class queue behind that lock, so
"count active holds, compare with `totalSeats`, insert" can never interleave. Because the lock is
held by the database rather than in process memory, it also holds when several backend processes
share one database (see the two-instance test below). Expiry is computed by comparing
`expires_at` with the current time inside every query, so a seat becomes available the instant
its hold expires with no job or API call involved. Details: [ASSUMPTIONS.md](ASSUMPTIONS.md).

## Tests

### Backend (xUnit, real PostgreSQL via Testcontainers)

Requires the .NET 8 SDK and a running Docker daemon (a disposable PostgreSQL container is started
automatically for the test run).

```bash
cd backend
dotnet test
```

The suite covers:

- **Concurrency** (required): 150 simultaneous applications for a 10-seat class yield exactly
  10 × `201` and 140 × `409`, and the class then reports 10 held / 0 available.
- **Two backend instances on one database** (bonus): two independent in-process hosts share
  the same PostgreSQL and fire 120 parallel applications for a 7-seat class; still exactly 7 succeed.
- Expiry without any API call, confirm idempotency, owner-only confirm/cancel, one active
  application per student per class, cancel releasing the seat, `GET /me/holds`, and a table of
  bad inputs that must all return `400`/`404`.

### Frontend (Vitest + React Testing Library)

Requires Node.js 20+.

```bash
cd frontend
npm install
npm test
```

Covers the countdown switching to *Expired* and disabling the pay button without reload, the
class table rendering seat counts and disabling *Apply* when full, and the API client's error
translation (including the server-unreachable case).

## Running without Docker (development)

```bash
# PostgreSQL
docker run -d --name admission-pg -p 5432:5432 -e POSTGRES_USER=admission -e POSTGRES_PASSWORD=admission -e POSTGRES_DB=admission postgres:16-alpine

# Backend (http://localhost:8080; connection string in appsettings.json matches the container above)
cd backend/src/SchoolAdmission.Api && dotnet run

# Frontend (http://localhost:3000; talks to http://localhost:8080 via public/config.js)
cd frontend && npm install && npm run dev
```

## Repository layout

```
backend/
  SchoolAdmission.sln
  Dockerfile
  src/SchoolAdmission.Api/        ASP.NET Core API (controllers, services, EF Core, migrations)
  tests/SchoolAdmission.Api.Tests/ xUnit integration tests (Testcontainers PostgreSQL)
frontend/
  Dockerfile, docker/              nginx image with runtime config injection
  src/                             React app (components, hooks, api client, tests)
docker-compose.yml
README.md · ASSUMPTIONS.md · AI_USAGE.md
```
