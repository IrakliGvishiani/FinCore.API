# FinCore

FinCore is a digital banking system simulator. Customers can open accounts in several currencies, transfer money, convert currencies at real exchange rates, issue virtual cards and pay with them in a demo shop. Managers and administrators monitor operations, review suspicious activity and audit every important action.

This repository contains the **backend** (ASP.NET Core Web API). The Angular client lives in a separate repository: [`fincore-client`](https://github.com/IrakliGvishiani/fincore-client).

> **This is a graduation project and a simulation. No real money is involved and nothing is connected to real payment networks.**

---

## Features

**Authentication and profiles**
- Registration with email confirmation (6-digit code), login, JWT access tokens and rotating refresh tokens
- Password reset by email link
- Profile management with photo upload (Cloudinary)
- Roles: `Customer`, `Manager`, `Admin`

**Accounts**
- Open accounts in GEL, USD, EUR and GBP, rename, close (balance must be zero)
- Deposits, paged account statement, per-currency analytics (income, expenses, monthly breakdown)
- Admins can freeze and unfreeze accounts

**Transfers**
- Transfer by account number or by the recipient's personal number (recipient accounts are shown masked)
- Transfer history, details and PDF receipts
- Saved recipients

**Currency conversion**
- Live exchange rates from the National Bank of Georgia (NBG) API
- Quote before converting, conversion fee, rate protection (the client sends the rate it was quoted)
- Conversion history and PDF receipts

**Virtual cards and simulated payments**
- Create virtual cards with daily and per-transaction limits, freeze, unfreeze, close
- The full card number is shown only once, at creation
- Simulated card payments with stored declines (limit exceeded, insufficient funds, card not active, ...)

**Monitoring and security**
- Manager panel: transfers, accounts, conversions and suspicious activity (review and resolve workflow)
- Admin panel: users (ban and unban), staff registration, freeze and unfreeze accounts, audit log
- Automatic detection of suspicious activity with a risk score
- Audit log for important actions (who did what, when, from which IP)

---

## Tech stack

| Area | Technology |
|---|---|
| Language / framework | C#, ASP.NET Core Web API (.NET 10) |
| Architecture | Clean Architecture |
| Database | Microsoft SQL Server, Entity Framework Core |
| Identity | ASP.NET Core Identity, JWT bearer tokens |
| Cache / token store | Redis (refresh tokens, email codes, rate limits, rates cache) |
| Files | Cloudinary (profile photos) |
| Email | SMTP (Brevo relay) |
| Containers | Docker, Docker Compose |
| Frontend | Angular (separate repository) |

---

## Architecture

The solution follows Clean Architecture. Dependencies point inwards: the Domain layer knows nothing about the other layers.

```
FinCore.sln
├── FinCore/                  API layer: controllers, middleware, Program.cs, configuration
├── FinCore.Application/      Use cases: services, DTOs, interfaces, application exceptions
├── FinCore.Domain/           Entities, enums, domain rules and domain exceptions
├── FinCore.Infrastructure/   EF Core DbContext, migrations, repositories, external APIs
└── FinCore.UnitTests/        Unit tests
```

Key design decisions:

- **Double-entry ledger.** Every money movement creates a `LedgerTransaction` with debit and credit `LedgerEntry` rows against customer accounts and bank ledger accounts (cash, FX, fees, card settlement). Balances can be reconciled from the ledger.
- **Idempotency.** Deposits, transfers, conversions and card payments require an idempotency key, so a retried request never executes twice. Replaying the same key with the same data returns the original result; the same key with different data returns `409`.
- **Concurrency.** Bank accounts use a row version (optimistic concurrency) and money operations run in serializable transactions.
- **Error handling.** A central middleware maps exceptions to HTTP status codes. Domain rule violations return `400` with a readable message, unexpected errors return a generic `500` and are logged.
- **Security.** Role-based authorization on every endpoint, banned users cannot refresh tokens and their access tokens are rejected, refresh tokens are rotated on every use, rate limits on sensitive endpoints (recipient lookup, confirmation emails).

---

## API overview

All responses use a common envelope: `{ message, result, isSuccess, statusCode }`. Enums are serialized as strings. Interactive documentation is available through Swagger in the Development environment (`/swagger`).

| Area | Base route | Access |
|---|---|---|
| Auth | `/api/auth` (register, confirm-email, login, refresh-token, forgot-password, reset-password, revoke-token) | Public. Staff registration requires `Admin` |
| Profile | `/api/profile` | Any authenticated user |
| Accounts | `/api/account` | Customer |
| Transfers | `/api/transfers` | Customer |
| Conversions | `/api/conversions` | Customer |
| Virtual cards | `/api/card` | Customer |
| Payments | `/api/payments` | Customer |
| Saved recipients | `/api/recipient` | Customer |
| Monitoring | `/api/manager` | Manager, Admin |
| Administration | `/api/admin` | Admin |

---

## Getting started

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Docker Compose v2
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (only if you want to run the API outside Docker or run tests)
- A [Cloudinary](https://cloudinary.com/) account (free tier) for profile photos
- An SMTP account for emails (the compose file is configured for the Brevo relay)

### 1. Clone the repositories

The Docker Compose file builds the Angular client from a sibling folder, so clone both repositories side by side:

```bash
git clone https://github.com/IrakliGvishiani/FinCore.API.git
git clone https://github.com/IrakliGvishiani/fincore-client.git
```

### 2. Create the `.env` file

Create a `.env` file next to `docker-compose.yml` in the `FinCore` folder. **Never commit this file.**

```env
ASPNETCORE_ENVIRONMENT=Development

MSSQL_SA_PASSWORD=<strong password: 8+ chars, upper, lower, digit, symbol>
JWT_SECRET=<random string, at least 64 characters>

CLOUDINARY_CLOUD_NAME=<value>
CLOUDINARY_API_KEY=<value>
CLOUDINARY_API_SECRET=<value>

SMTP_SENDER=<verified sender address>
SMTP_USERNAME=<value>
SMTP_PASSWORD=<value>

# Administrator created on first start
ADMIN_EMAIL=<value>
ADMIN_PASSWORD=<strong password>
ADMIN_PERSONAL_NUMBER=<11 digits>
ADMIN_PHONE_NUMBER=<+995XXXXXXXXX>
ADMIN_FIRST_NAME=<value>
ADMIN_LAST_NAME=<value>

# Used in links inside emails (confirmation, password reset)
APP_BASE_URL=http://localhost:8091
FRONTEND_BASE_URL=http://localhost:8091
```

Tip: generate secrets with `openssl rand -base64 64` and avoid the `$` character in values, because Docker Compose treats it as a variable.

### 3. Start everything

```bash
docker compose up -d --build
docker compose ps
docker compose logs -f fincore.api
```

On the first start the API applies the database migrations automatically and creates the administrator from the `ADMIN_*` settings.

| Service | Address (host) |
|---|---|
| Web client (nginx) | http://localhost:8091 |
| API | http://localhost:8080 (bound to 127.0.0.1) |
| Swagger (Development only) | http://localhost:8080/swagger |
| SQL Server | `127.0.0.1:1434` |

Open http://localhost:8091 and sign in with the administrator account. Managers and further admins are created by an admin in the Administration section.

### Running the API without Docker

Start only the infrastructure containers and run the API with the .NET SDK:

```bash
docker compose up -d sqlserver redis
dotnet user-secrets init --project FinCore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1434;Database=FinCoreDb;User Id=sa;Password=<password>;TrustServerCertificate=True;" --project FinCore
dotnet user-secrets set "Jwt:Secret" "<64+ character secret>" --project FinCore
dotnet run --project FinCore
```

Set the remaining settings (Redis, Cloudinary, email, admin seed) the same way or through environment variables. For the client, run `ng serve` in `fincore-client`; its dev proxy forwards `/api` to the API.

### Database migrations

Migrations are applied automatically at startup. To add a new migration:

```bash
dotnet ef migrations add <MigrationName> --project FinCore.Infrastructure --startup-project FinCore
```

The design-time tooling builds the host, so the same configuration values (for example the Cloudinary settings) must be available as environment variables when you run this command.

### Tests

```bash
dotnet test
```

---

## Deployment

The production setup runs on a single Ubuntu server with Docker Compose: SQL Server, Redis, the API and the nginx container serving the Angular client. HTTPS is terminated by Caddy on the host, which proxies to the client container; the client proxies `/api` to the API on the same origin, so no CORS configuration is needed.

- Only ports 22, 80 and 443 are open in the firewall; the API and SQL Server are bound to `127.0.0.1`.
- Secrets live only in the `.env` file on the server.
- Update: `git pull` in both repositories, then `docker compose up -d --build`.

Detailed notes for the client container and the reverse proxy are in the `fincore-client` repository.

---

## Known limitations

- Card payments are charged in the currency of the card's account; there is no automatic conversion for shop prices in another currency (the demo shop prices items in GEL).
- Date filters compare UTC days, so operations made between 00:00 and 04:00 in Georgia can fall on the previous day.
- A banned user's already-issued access token is rejected through a short-lived ban cache; existing refresh tokens are blocked at refresh time rather than revoked individually.
- Scheduled transfers and changing the password while logged in are not implemented (use forgot / reset password).

---

## Author

Graduation project by **<Irakli Gvishiani>**.
