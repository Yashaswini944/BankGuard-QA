
# BankGuard QA — API Test Plan

## 1. Objective

Validate the correctness of the BankGuard Banking API
through automated functional, negative, and boundary tests.

## 2. System Under Test

Application: BankGuard API

Technology: ASP.NET Core Web API

Testing Framework: NUnit

HTTP Client: RestSharp

## 3. Testing Scope

The current test suite covers:

- Account creation
- Account retrieval
- HTTP response validation
- Invalid account requests
- Negative balance validation
- Zero and minimum positive balance validation
- Customer name validation
- Name length boundaries

## 4. Test Cases

| ID | Test Scenario | Expected Result |
|---|---|---|
| TC-001 | Create account with valid data | 201 Created |
| TC-002 | Retrieve an existing account | 200 OK |
| TC-003 | Retrieve nonexistent account | 404 Not Found |
| TC-004 | Create account with balance -1 | 400 Bad Request |
| TC-005 | Create account with balance -100 | 400 Bad Request |
| TC-006 | Create account with balance -5000 | 400 Bad Request |
| TC-007 | Create account with balance 0 | 201 Created |
| TC-008 | Create account with balance 0.01 | 201 Created |
| TC-009 | Create account with empty owner name | 400 Bad Request |
| TC-010 | Create account with whitespace name | 400 Bad Request |
| TC-011 | Create account with 100-character name | 201 Created |
| TC-012 | Create account with 101-character name | 400 Bad Request |

## 5. Test Execution

Run the BankGuard API locally.

Execute:

dotnet test tests/BankGuard.ApiTests/BankGuard.ApiTests.csproj

## 6. Test Results

Latest verified local execution:

- Total: 12
- Passed: 12
- Failed: 0

## 7. Known Limitations

- Accounts are currently stored in memory.
- Database persistence testing is not yet implemented.
- Authentication and authorization testing are not yet implemented.
- Banking transfer testing is not yet implemented.
- Concurrency and reliability testing are planned.

## 8. Future Testing

- Database integration testing
- Banking transfers
- Transaction consistency
- Duplicate request handling
- Authorization testing
- Concurrency testing
- Automated CI/CD execution
