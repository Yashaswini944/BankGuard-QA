
# BankGuard QA — Banking Transfer Requirements

## 1. Project Overview

BankGuard QA is an API automation testing project
built using C#, ASP.NET Core Web API, NUnit,
RestSharp, SQL Server, and GitHub Actions.

This phase introduces simulated banking transfers
and database-backed transaction validation.

All accounts and transactions use fictional data.

## 2. Business Objective

Provide reliable account-to-account transfers while
ensuring data consistency, preventing duplicate
processing, and handling invalid transactions safely.

## 3. Functional Requirements

### BR-001: Account Creation

The system shall allow creating a bank account with:

- Unique account ID
- Account owner name
- Initial balance

The initial balance must not be negative.

### BR-002: Transfer Funds

The system shall allow transferring a valid amount
between two existing fictional bank accounts.

A successful transfer must:

- Deduct the amount from the source account.
- Add the amount to the destination account.
- Create a transaction record.
- Return a unique transaction ID.
- Preserve the combined balance of both accounts.

### BR-003: Insufficient Funds

The system shall reject a transfer when the source
account does not have sufficient funds.

Expected behavior:

- Return HTTP 409 Conflict.
- Do not modify either account balance.
- Do not create a completed transfer record.

### BR-004: Invalid Transfer Amount

The system shall reject:

- Zero transfer amounts.
- Negative transfer amounts.
- Amounts with more than two decimal places.

Expected response: HTTP 400 Bad Request.

### BR-005: Invalid Account

The system shall reject transfers if the source
or destination account does not exist.

Expected response: HTTP 404 Not Found.

### BR-006: Same-Account Transfer

The system shall reject transfers when the source
and destination account IDs are identical.

Expected response: HTTP 400 Bad Request.

### BR-007: Transaction Atomicity

Both balance updates and the transfer record must
be committed as one database transaction.

If any required operation fails, the entire
transaction must be rolled back.

### BR-008: Duplicate Transfer Prevention

Each transfer request must include a unique
idempotency key.

When the same key and request data are submitted
again, the system must return the original result
without transferring money a second time.

Reusing the same key with different transfer data
must return HTTP 409 Conflict.

### BR-009: Transaction History

The system shall allow retrieving a completed
transaction by its unique transaction ID.

### BR-010: Concurrent Transfers

When concurrent requests affect the same account,
the system must prevent overdrawing the account
and maintain consistent database balances.

## 4. Proposed API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | /api/accounts | Create account |
| GET | /api/accounts/{id} | Retrieve account |
| POST | /api/transfers | Transfer funds |
| GET | /api/transfers/{id} | Get transfer details |

The transfer endpoint will use an Idempotency-Key
HTTP header.

**Implementation Note:** The account creation and retrieval
endpoints are implemented and tested. The transfer creation
and transaction retrieval endpoints are planned for Phase 7
and are not yet available.


## 5. Sample Transfer Request

POST /api/transfers

Header:
Idempotency-Key: sample-transfer-001

Request body:

```json
{
  "fromAccountId": "SOURCE_ACCOUNT_ID",
  "toAccountId": "DESTINATION_ACCOUNT_ID",
  "amount": 100.00
}
```

The account IDs above are placeholders for
fictional test accounts.

## 6. Acceptance Criteria

### TC-013: Successful Transfer

Given:
- Account A contains 1000.00
- Account B contains 200.00

When:
- 100.00 is transferred from A to B

Then:
- API returns 201 Created
- Account A contains 900.00
- Account B contains 300.00
- A completed transaction is recorded

### TC-014: Insufficient Funds

Given:
- Account A contains 50.00

When:
- A transfer of 100.00 is requested

Then:
- API returns 409 Conflict
- Account balances remain unchanged

### TC-015: Zero Amount

When a transfer of 0.00 is requested:

- API returns 400 Bad Request.
- No balances are modified.

### TC-016: Negative Amount

When a negative transfer amount is requested:

- API returns 400 Bad Request.
- No balances are modified.

### TC-017: Nonexistent Account

When either account ID does not exist:

- API returns 404 Not Found.
- No balances are modified.

### TC-018: Same-Account Transfer

When source and destination account IDs match:

- API returns 400 Bad Request.
- No balances are modified.

### TC-019: Duplicate Transfer Request

Given:
- A transfer has already completed.

When:
- An identical request is submitted with the
  same idempotency key.

Then:
- The original transaction is returned.
- Balances are not updated again.

### TC-020: Concurrent Transfers

Given:
- Account A contains 100.00.

When:
- Two simultaneous requests each attempt
  to transfer 80.00 from Account A.

Then:
- At most one transfer succeeds.
- Account A never becomes negative.
- All recorded balances remain consistent.

### TC-021: Database Rollback

Given:
- A simulated database failure occurs during
  the transfer operation.

Then:
- Neither balance is partially updated.
- No completed transfer is recorded.

### TC-022: Transaction Retrieval

Given:
- A transfer was completed successfully.

When:
- The transaction is retrieved by ID.

Then:
- API returns 200 OK.
- Transaction data matches the completed transfer.

## 7. Test Categories

- Functional Testing
- API Testing
- Data-Driven Testing
- Negative Testing
- Boundary Value Testing
- Database Integration Testing
- Concurrency Testing
- Regression Testing
- Failure Recovery Testing

## 8. Technology Stack

- C#
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- NUnit
- RestSharp
- GitHub Actions
- Git


## 9. Current Implementation Status

### Completed — Phase 6

The following components have been implemented and verified:

- ASP.NET Core Web API for bank account management.
- Account creation and retrieval endpoints.
- SQL Server database integration using Entity Framework Core.
- BankAccount and BankTransaction database models.
- BankGuardDbContext and EF Core database migrations.
- Persistent account storage in SQL Server.
- Input validation for account creation.
- 12 automated API tests using NUnit and RestSharp.
- Functional, negative, data-driven, and boundary-value testing.
- GitHub Actions CI/CD pipeline.
- Automated SQL Server setup and database migrations in GitHub Actions.
- All 12 automated API tests passing in CI.
- TRX test report generation and artifact upload.

### Planned — Phase 7

The following functionality is defined in this document but has not yet been implemented:

- Account-to-account money transfers.
- POST /api/transfers endpoint.
- GET /api/transfers/{id} endpoint.
- Atomic database transaction handling.
- Insufficient-funds validation.
- Invalid transfer amount validation.
- Same-account transfer prevention.
- Duplicate transfer prevention using idempotency keys.
- Concurrent transaction handling.
- Database rollback testing.
- Transaction history verification.
- Automated database-level assertions.

### Current Testing Limitations

- The existing automated tests cover account creation, retrieval, and input validation.
- Automated tests do not yet cover banking transfers.
- The Transfers database table exists, but the transfer API and business logic are not yet implemented.
- Direct SQL Server database assertions are not yet part of the automated test suite.
- Authentication and authorization are not yet implemented for banking operations.
- The application uses fictional accounts and test data only.

### Phase 7 Objective

Implement a reliable simulated banking transfer workflow and develop automated tests that verify transactional consistency, failure handling, duplicate-request prevention, and concurrency behavior.

All new functionality must preserve the existing 12 passing regression tests.
