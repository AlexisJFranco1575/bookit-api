# Bookit API

Appointment booking API for small businesses such as barbershops, clinics and studios: services, weekly schedules, bookable time slots and email reminders.

> Work in progress. Milestones 1 and 2 (domain, availability engine, persistence and race-free booking) are done.

## Why this project

The hard part of a booking system is not the CRUD, it is time and concurrency:

- **Time zones.** Every business has its own. Working hours are read in the business's local time and all instants are stored in UTC, so daylight-saving changes are handled (there is a test for the day clocks spring forward).
- **Half-open intervals.** An appointment ending at 10:00 and another starting at 10:00 never overlap.
- **No double booking, even under load.** A slot has to be offered by the availability engine and also survive an atomic check at insert time. See below.

## How double booking is prevented

SQL Server cannot express "these time ranges must not overlap" as a constraint, so a unique index alone is not enough: a booking from 09:00 to 10:00 and one from 09:30 to 10:30 start at different times and would both pass.

Instead, `AppointmentRepository.TryAddAsync` opens a transaction, takes an exclusive application lock for that one provider (`sp_getapplock`, released automatically on commit or rollback), checks for overlapping booked appointments and inserts. Requests for the same provider are serialized; requests for different providers never wait for each other.

A filtered unique index on `(ProviderId, StartsAt)` for booked appointments stays behind it as a safety net.

The integration tests run against a real SQL Server (Testcontainers) and fire 20 simultaneous requests for the same slot: exactly one wins.

## Roadmap

| Milestone | Scope | Status |
| --- | --- | --- |
| 1 | Domain: time ranges, weekly schedules, slot calculator, appointment lifecycle | Done |
| 2 | EF Core and SQL Server persistence, booking without double booking, concurrency tests | Done |
| 3 | REST API, owner authentication, public booking endpoints | Planned |
| 4 | Email reminders with a background worker | Planned |
| 5 | Docker, CI/CD, Azure deployment | Planned |
| 6 | Angular booking page and business dashboard | Planned |

## Project layout

| Folder | What it contains |
| --- | --- |
| `src/Bookit.Domain` | Entities and rules. No dependencies on frameworks or I/O. |
| `src/Bookit.Application` | Use cases (catalog, availability, booking) and the ports they need. |
| `src/Bookit.Infrastructure` | EF Core, SQL Server and the repositories. |
| `tests/Bookit.Domain.Tests` | Unit tests for the domain. |
| `tests/Bookit.Application.Tests` | Use-case tests with in-memory fakes. |
| `tests/Bookit.Infrastructure.IntegrationTests` | Tests against a real SQL Server in Docker. |

## Run the tests

Requires the .NET 10 SDK. The integration tests also need Docker running. Then run `dotnet test` from the repository root.