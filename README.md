# Bookit API

Appointment booking for small businesses (barbershops, clinics, studios): owners publish their services and working hours, customers pick a free slot and book it, with no account needed.

Built with **.NET 10**, **Clean Architecture**, **EF Core + SQL Server** and an automated test suite that includes real-database and end-to-end tests.

## Status

| Milestone | Scope | State |
| --- | --- | --- |
| 1 | Domain model and the slot calculator (time zones, DST, working hours) | Done |
| 2 | Persistence, booking service and the double-booking guarantee | Done |
| 3 | REST API: owner accounts (JWT), catalog, availability, booking, owner agenda | Done |
| 4 | Email confirmations and reminders (background worker) | Planned |
| 5 | Docker, CI/CD and Azure deployment | Planned |
| 6 | Angular booking page and owner dashboard | Planned |

## What it does

- **Owners** sign up, create businesses, services and providers (with weekly working hours) and read the agenda of their business.
- **Customers** browse a business, see the free slots of a provider for a service, book and cancel. No account: the appointment id is a random secret (Guid v7, 74 random bits).
- **Time zones done right**: every instant is stored in UTC; each business has an IANA time zone, so "Monday 09:00" means the right moment all year, including daylight-saving changes.

## Architecture

```text
Bookit.Api             minimal APIs, JWT, problem+json errors, rate limits, OpenAPI (Scalar)
   |
Bookit.Infrastructure  EF Core + SQL Server, repositories, PBKDF2 password hasher
   |
Bookit.Application     use cases (auth, catalog, booking, agenda), DTOs, ports
   |
Bookit.Domain          entities, SlotCalculator, TimeRange (no dependencies)
```

Dependencies point inwards: the domain knows nothing about the database or HTTP.

## How double booking is prevented

Two customers can click the same slot at the same instant. The booking is protected in three layers:

1. **Slot calculator**: only slots it offers can be booked, so off-grid or out-of-hours times are rejected.
2. **Per-provider lock**: `sp_getapplock` (exclusive, transaction-scoped) serialises bookings of one provider, so the overlap check and the insert are one indivisible step. Different providers never block each other.
3. **Unique index** on `(ProviderId, StartsAt) WHERE Status = 'Booked'` as a last line of defence in the database itself.

Tests fire 20 simultaneous bookings for one slot against a real SQL Server, both directly on the repository and over HTTP: exactly one wins and the rest get `409 Conflict`.

## API

Interactive documentation (Scalar) is served at `/scalar` in Development.

| Method and route | Access | Purpose |
| --- | --- | --- |
| `POST /api/auth/register` | Public, rate limited | Create an owner account |
| `POST /api/auth/login` | Public, rate limited | Get a JWT access token |
| `POST /api/businesses` | Owner | Create a business |
| `GET /api/businesses/mine` | Owner | List my businesses |
| `GET /api/businesses/{id}` | Public | Show a business |
| `GET, POST /api/businesses/{id}/services` | Read: public. Create: owner | Services |
| `GET, POST /api/businesses/{id}/providers` | Read: public. Create: owner | Providers and weekly hours |
| `GET /api/businesses/{id}/appointments?from&to` | Owner | Agenda for a range of local days (max 62) |
| `GET /api/availability?providerId&serviceId&from&to` | Public | Free slots |
| `POST /api/appointments` | Public, rate limited | Book |
| `GET /api/appointments/{id}` | Public (id is the secret) | Show an appointment |
| `POST /api/appointments/{id}/cancel` | Public (id is the secret) | Cancel |
| `GET /health` | Public | Health check |

Errors use `application/problem+json`: `400` malformed request, `401` bad credentials or token, `404` not found (also for businesses owned by someone else), `409` conflict (slot taken, email already registered), `422` business rule broken, `429` too many requests.

## Security decisions

- Passwords: PBKDF2-HMAC-SHA256, 600,000 iterations, random salt, constant-time comparison.
- Login answers the same error and does the same hashing work for unknown emails and wrong passwords.
- JWT signing key must be at least 32 bytes; the app refuses to start without it.
- A business owned by someone else is reported as "not found", so ids cannot be probed.
- Rate limits on registration, login and booking; CORS closed unless origins are configured.

## Run it locally

Requirements: .NET 10 SDK and Docker.

```powershell
docker run --name bookit-sql -e 'ACCEPT_EULA=Y' -e 'MSSQL_SA_PASSWORD=Bookit_Dev_2026!' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

dotnet user-secrets set 'ConnectionStrings:Bookit' 'Server=localhost,1433;Database=Bookit;User Id=sa;Password=Bookit_Dev_2026!;TrustServerCertificate=True;Encrypt=False' --project src/Bookit.Api
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set 'Jwt:SigningKey' $key --project src/Bookit.Api

dotnet run --project src/Bookit.Api
```

Open http://localhost:5080/scalar. In Development the database migrations are applied on startup.

## Tests

```powershell
dotnet test
```

| Project | What it covers | Needs Docker |
| --- | --- | --- |
| `Bookit.Domain.Tests` | Slot calculator, DST, working hours, entities | No |
| `Bookit.Application.Tests` | Auth, catalog, booking and agenda use cases with fakes | No |
| `Bookit.Infrastructure.IntegrationTests` | Repositories, migrations, locks and the unique index on real SQL Server, password hasher | Yes |
| `Bookit.Api.IntegrationTests` | The whole API over HTTP: booking flow, ownership, error mapping, concurrency, rate limits | Yes |

CI builds, audits packages for known vulnerabilities (including transitive ones) and runs every test on each push and pull request.

## Project layout

| Path | Contents |
| --- | --- |
| `src/Bookit.Domain` | Entities and pure domain services |
| `src/Bookit.Application` | Use cases, DTOs and ports |
| `src/Bookit.Infrastructure` | EF Core, repositories, security |
| `src/Bookit.Api` | HTTP layer |
| `tests/` | The four test projects above |
| `.github/workflows/ci.yml` | Continuous integration |

## License

MIT