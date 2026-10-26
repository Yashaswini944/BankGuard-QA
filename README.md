
# BankGuard QA

**Automated Banking API Testing Framework**

**C# · .NET 10 · NUnit · RestSharp · ASP.NET Core Web API · Entity Framework Core · SQL Server · GitHub Actions**

[GitHub Repository](https://github.com/Yashaswini944/BankGuard-QA) · [CI Workflows](https://github.com/Yashaswini944/BankGuard-QA/actions) · [Test Plan](docs/TestPlan.md) · [Transfer Requirements](docs/TransferRequirements.md)

## Overview

BankGuard QA is a software testing portfolio project built around a **simulated banking REST API**. Its purpose is to demonstrate test automation design, API validation, boundary-value analysis, data-driven tests, database-backed integration, and repeatable continuous integration.

The repository includes two separate .NET projects:

- **BankGuard.Api** — the system under test (SUT), an ASP.NET Core Web API backed by SQL Server and Entity Framework Core.
- **BankGuard.ApiTests** — an NUnit and RestSharp automation suite that calls the API over HTTP and checks its behavior.

All account names, balances, and transactions are fictional. **This is a learning and testing application, not a production banking system.**

## Current Status

**Completed through Phase 6 — SQL Server-backed account testing**

| Area | Status | Details |
| --- | --- | --- |
| Account creation API | Implemented | `POST /api/accounts` |
| Account retrieval API | Implemented | `GET /api/accounts/{id}` |
| Account persistence | Implemented | SQL Server via EF Core |
| Database schema | Implemented | Accounts and Transfers tables via migrations |
| API automation | Implemented | 12 NUnit / RestSharp test cases |
| Continuous integration | Implemented | GitHub Actions with isolated SQL Server |
| Test evidence | Implemented | Console results and downloadable TRX artifacts |
| Money transfer endpoints | **Planned** | Phase 7 |
| Concurrency, rollback, and idempotency tests | **Planned** | Phase 7 |
| Authentication / authorization tests | **Planned** | Not implemented |

The **Transfers** table and `BankTransaction` model exist as database foundations; **the money-transfer business logic and endpoints are not implemented yet**.

## Technology Stack

| Layer | Tools and technologies |
| --- | --- |
| Programming | C#, .NET 10 |
| System under test | ASP.NET Core Web API |
| Automated tests | NUnit, RestSharp |
| Data access | Entity Framework Core |
| Database | Microsoft SQL Server |
| Schema management | EF Core migrations (`dotnet-ef` 10.x) |
| CI/CD | GitHub Actions |
| Test reports | TRX / GitHub Actions artifacts |
| Version control | Git and GitHub |
| API exploration | Postman |

## Architecture

```text
NUnit tests (BankGuard.ApiTests)
            |
            | RestSharp HTTP requests
            v
ASP.NET Core API (BankGuard.Api)
            |
            | Dependency injection
            v
       EF Core DbContext
            |
            v
      SQL Server database
      (BankGuardQaDev locally)
```

Tests run against a **real HTTP endpoint** rather than calling controller methods directly. In GitHub Actions, the workflow starts a temporary SQL Server container, applies the EF Core migrations, launches the API, runs the test suite, and uploads the results.

## Repository Structure

```text
BankGuard-QA/
├── .config/
│   └── dotnet-tools.json             # Local EF Core CLI tool manifest
├── .github/
│   └── workflows/
│       └── api-tests.yml             # Automated CI workflow
├── docs/
│   ├── TestPlan.md                   # Existing test scenarios and scope
│   └── TransferRequirements.md       # Proposed Phase 7 requirements
├── src/
│   └── BankGuard.Api/
│       ├── Controllers/
│       │   └── AccountsController.cs
│       ├── Data/
│       │   ├── BankGuardDbContext.cs
│       │   └── Migrations/
│       ├── Models/
│       │   ├── BankAccount.cs
│       │   └── BankTransaction.cs
│       ├── Program.cs
│       └── BankGuard.Api.csproj
├── tests/
│   └── BankGuard.ApiTests/
│       ├── AccountsApiTests.cs
│       └── BankGuard.ApiTests.csproj
├── .gitignore
├── BankGuardQA.slnx
└── README.md
```

Generated `bin/`, `obj/`, and `TestResults/` content is excluded from Git tracking.

## Implemented API Endpoints

### Create an Account

`POST /api/accounts`

**Request**

```json
{
  "ownerName": "Test Customer",
  "initialBalance": 1000.00
}
```

**Expected success:** `201 Created`

**Example response** (the UUID is illustrative):

```json
{
  "id": "9ed45171-45b4-40fd-8a44-dcf9ee16b204",
  "ownerName": "Test Customer",
  "balance": 1000.00
}
```

Current validation includes nonempty owner names, a maximum of 100 characters, nonnegative balances, and balance precision limits.

### Retrieve an Account

`GET /api/accounts/{id}`

| Condition | Expected response |
| --- | --- |
| Account exists | `200 OK` and account data |
| Account does not exist | `404 Not Found` |

The account records are retrieved from SQL Server using Entity Framework Core.

## Automated Regression Tests

The current suite has **12 NUnit test cases**:

| ID | Scenario | Expected result |
| --- | --- | --- |
| TC-001 | Create account with valid data | `201` |
| TC-002 | Retrieve account created by the test | `200` |
| TC-003 | Retrieve a nonexistent account | `404` |
| TC-004 | Initial balance `-1` | `400` |
| TC-005 | Initial balance `-100` | `400` |
| TC-006 | Initial balance `-5000` | `400` |
| TC-007 | Initial balance `0` | `201` |
| TC-008 | Initial balance `0.01` | `201` |
| TC-009 | Empty owner name | `400` |
| TC-010 | Whitespace-only owner name | `400` |
| TC-011 | Owner name length `100` | `201` |
| TC-012 | Owner name length `101` | `400` |

**Testing practices demonstrated:**

- Positive and negative testing
- Boundary-value analysis and parameterized `[TestCase]` tests
- HTTP response status and JSON payload assertions
- Reusable RestSharp client setup
- Unique test account names to avoid inter-test dependence
- Regression execution through CI

> **Scope note:** The current suite validates database-backed behavior through the API. It does **not** yet include independent SQL assertions, automated restart-persistence checks, or transaction/concurrency tests.

See [docs/TestPlan.md](docs/TestPlan.md) for the test plan.

## Run Locally (Windows / PowerShell)

### Prerequisites

- .NET 10 SDK
- A reachable Microsoft SQL Server instance
- Permission to create or modify the dedicated development database
- Git
- Postman (optional)

These instructions assume your local SQL Server is reachable at `localhost` using Windows Authentication. Change the connection string if your SQL Server instance is different.

### 1. Clone the Repository

```powershell
git clone https://github.com/Yashaswini944/BankGuard-QA.git
cd BankGuard-QA
```

### 2. Restore and Build

```powershell
dotnet restore BankGuardQA.slnx
dotnet build BankGuardQA.slnx
```

### 3. Create the Development Database

If `sqlcmd` is installed and your Windows account has permission, run:

```powershell
sqlcmd -S localhost -E -C -Q "IF DB_ID('BankGuardQaDev') IS NULL CREATE DATABASE BankGuardQaDev;"
```

`-C` trusts the SQL Server certificate and is used here **only for a trusted local development server**. Do not use certificate-trust bypasses as a production security configuration.

### 4. Configure the Connection String Using .NET User Secrets

```powershell
dotnet user-secrets init --project .\src\BankGuard.Api\BankGuard.Api.csproj
dotnet user-secrets set "ConnectionStrings:BankGuardDb" "Server=localhost;Database=BankGuardQaDev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True" --project .\src\BankGuard.Api\BankGuard.Api.csproj
```

If User Secrets have already been initialized, skip `init` and keep the existing identifier. User Secrets are a **development-only configuration mechanism**; do not commit passwords or production secrets.

### 5. Restore EF Core CLI and Apply Migrations

The local tool manifest is stored at **`.config/dotnet-tools.json`**.

From the repository root:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef --version
```

Then update the database:

```powershell
dotnet tool run dotnet-ef database update --project .\src\BankGuard.Api\BankGuard.Api.csproj --startup-project .\src\BankGuard.Api\BankGuard.Api.csproj
```

This applies the checked-in migrations. It should create the database tables including `Accounts`, `Transfers`, and `__EFMigrationsHistory` if they do not already exist.

### 6. Start the API — Terminal 1

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project .\src\BankGuard.Api\BankGuard.Api.csproj --no-launch-profile -- --urls http://localhost:5105
```

Keep this terminal running. The expected address is **`http://localhost:5105`**.

### 7. Run Tests — Terminal 2

Open a separate terminal at the repository root:

```powershell
dotnet test .\tests\BankGuard.ApiTests\BankGuard.ApiTests.csproj --logger "console;verbosity=normal"
```

To generate a TRX test report:

```powershell
dotnet test .\tests\BankGuard.ApiTests\BankGuard.ApiTests.csproj --logger "trx;LogFileName=BankGuard-Results.trx"
```

The API must be running, with SQL Server accessible, while the tests execute. Tests target `http://localhost:5105` by default; use the `BANKGUARD_API_BASE_URL` environment variable to override the URL.

### 8. Optional Database Verification

```powershell
sqlcmd -S localhost -E -C -d BankGuardQaDev -Q "SELECT TOP (10) Id, OwnerName, Balance FROM dbo.Accounts ORDER BY CreatedAtUtc DESC;"
```

This query is read-only. The tests create fictional accounts, so repeated test runs can add records to your local development database.

## Continuous Integration — GitHub Actions

Workflow: [`.github/workflows/api-tests.yml`](.github/workflows/api-tests.yml)

The workflow:

1. Checks out the repository and configures .NET 10.
2. Restores dependencies and builds the solution.
3. Starts an isolated SQL Server container with temporary CI-only credentials.
4. Waits for SQL Server readiness.
5. Applies EF Core migrations to a CI database.
6. Starts BankGuard API and checks that it responds.
7. Runs NUnit regression tests via RestSharp.
8. Uploads TRX test reports as GitHub Actions artifacts.

The SQL Server used in CI is **separate from the developer's local database**.

### Confirmed Regression Evidence

The 12-test regression suite was reported passing during local development and in the SQL Server-enabled GitHub Actions workflow:

```text
Total tests: 12
Passed: 12
Failed: 0
Skipped: 0
```

Review the latest run and its downloadable test artifact on the [Actions page](https://github.com/Yashaswini944/BankGuard-QA/actions). These results cover the implemented account scenarios, **not the planned transfer features**.

## QA Documentation

- [API Test Plan](docs/TestPlan.md): coverage, cases, and testing limitations.
- [Banking Transfer Requirements](docs/TransferRequirements.md): business requirements and acceptance criteria for Phase 7.

## Known Limitations

- There are only two implemented account API endpoints; money transfer endpoints are planned.
- Independent database assertions have not been automated.
- Persistence across API restarts has not been automated as a test case.
- Authentication, authorization, account ownership controls, and auditing are not implemented.
- The API is a development simulation; it must **not** be used with real banking accounts or personal financial data.
- Concurrency, rollback, performance, security, and idempotency testing are planned rather than completed.

## Roadmap

**Phase 7 — Transaction and reliability test automation**

- Implement account-to-account transfers using transactional database operations.
- Test insufficient funds, invalid amounts, missing accounts, and same-account transfers.
- Implement and test idempotency keys for duplicate transfer requests.
- Verify database rollback on simulated failures.
- Add concurrency tests and protect against inconsistent balances.
- Add direct database verification and extend CI reporting.

A feature will be marked **implemented** only after the relevant code, reproducible tests, and CI checks are complete.

## Why This Project Matters

BankGuard QA demonstrates the ability to create and maintain an API testing framework, validate expected and invalid user input, integrate a test environment with a relational database, and run repeatable regression tests through CI/CD. It is designed as portfolio evidence for **Junior QA Automation Engineer, API Test Engineer, Software Test Engineer, and entry-level SDET** roles.

## Author

**Yashaswini Halepalya Ravikumar**

[GitHub: Yashaswini944](https://github.com/Yashaswini944)