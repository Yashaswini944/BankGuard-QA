
using System;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using RestSharp;

namespace BankGuard.ApiTests;

[TestFixture]
public class AccountsApiTests
{
    private RestClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // =====================================================
    // TEST SETUP AND CLEANUP
    // =====================================================

    [OneTimeSetUp]
    public void Setup()
    {
        string baseUrl =
            Environment.GetEnvironmentVariable(
                "BANKGUARD_API_BASE_URL")
            ?? "http://localhost:5105";

        _client = new RestClient(baseUrl);
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        _client.Dispose();
    }

    // =====================================================
    // TEST 1: CREATE ACCOUNT - VALID DATA
    // =====================================================

    [Test]
    public async Task CreateAccount_ValidData_Returns201()
    {
        string ownerName = $"QA Customer {Guid.NewGuid():N}";

        var request = new RestRequest(
            "api/accounts",
            Method.Post);

        request.AddJsonBody(new
        {
            ownerName,
            initialBalance = 1000m
        });

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            $"Response: {response.Content}; Error: {response.ErrorMessage}");

        Assert.That(
            response.Content,
            Is.Not.Null.And.Not.Empty);

        var account = JsonSerializer.Deserialize<BankAccount>(
            response.Content!,
            JsonOptions);

        Assert.That(account, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                account!.Id,
                Is.Not.EqualTo(Guid.Empty));

            Assert.That(
                account.OwnerName,
                Is.EqualTo(ownerName));

            Assert.That(
                account.Balance,
                Is.EqualTo(1000m));
        });
    }

    // =====================================================
    // TEST 2: GET EXISTING ACCOUNT
    // =====================================================

    [Test]
    public async Task GetAccount_ExistingId_Returns200()
    {
        var createdAccount = await CreateTestAccount();

        var request = new RestRequest(
            $"api/accounts/{createdAccount.Id}",
            Method.Get);

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            $"Response: {response.Content}; Error: {response.ErrorMessage}");

        Assert.That(
            response.Content,
            Is.Not.Null.And.Not.Empty);

        var account = JsonSerializer.Deserialize<BankAccount>(
            response.Content!,
            JsonOptions);

        Assert.That(account, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                account!.Id,
                Is.EqualTo(createdAccount.Id));

            Assert.That(
                account.OwnerName,
                Is.EqualTo(createdAccount.OwnerName));

            Assert.That(
                account.Balance,
                Is.EqualTo(createdAccount.Balance));
        });
    }

    // =====================================================
    // TEST 3: GET NONEXISTENT ACCOUNT
    // =====================================================

    [Test]
    public async Task GetAccount_InvalidId_Returns404()
    {
        Guid invalidId = Guid.NewGuid();

        var request = new RestRequest(
            $"api/accounts/{invalidId}",
            Method.Get);

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));

        Assert.That(
            response.Content,
            Is.Not.Null.And.Not.Empty);

        using var json = JsonDocument.Parse(
            response.Content!);

        string? message = json.RootElement
            .GetProperty("message")
            .GetString();

        Assert.That(
            message,
            Is.EqualTo("Account not found."));
    }

    // =====================================================
    // TEST 4-6: DATA-DRIVEN NEGATIVE BALANCE TESTING
    // =====================================================

    [TestCase(-1)]
    [TestCase(-100)]
    [TestCase(-5000)]
    public async Task CreateAccount_NegativeBalance_Returns400(
        int initialBalance)
    {
        var request = new RestRequest(
            "api/accounts",
            Method.Post);

        request.AddJsonBody(new
        {
            ownerName = "Invalid Balance Customer",
            initialBalance
        });

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest),
            $"Expected 400 for balance {initialBalance}, " +
            $"but received {(int)response.StatusCode}. " +
            $"Response: {response.Content}");
    }

    // =====================================================
    // TEST 7-8: VALID BALANCE BOUNDARY TESTING
    // =====================================================

    [TestCase("0")]
    [TestCase("0.01")]
    public async Task CreateAccount_ValidBalanceBoundary_Returns201(
        string balanceText)
    {
        decimal initialBalance = decimal.Parse(
            balanceText,
            CultureInfo.InvariantCulture);

        string ownerName =
            $"Boundary Customer {Guid.NewGuid():N}";

        var request = new RestRequest(
            "api/accounts",
            Method.Post);

        request.AddJsonBody(new
        {
            ownerName,
            initialBalance
        });

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            $"Response: {response.Content}");

        Assert.That(
            response.Content,
            Is.Not.Null.And.Not.Empty);

        var account = JsonSerializer.Deserialize<BankAccount>(
            response.Content!,
            JsonOptions);

        Assert.That(account, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                account!.Id,
                Is.Not.EqualTo(Guid.Empty));

            Assert.That(
                account.OwnerName,
                Is.EqualTo(ownerName));

            Assert.That(
                account.Balance,
                Is.EqualTo(initialBalance));
        });
    }

    // =====================================================
    // TEST 9-10: INVALID CUSTOMER NAME TESTING
    // =====================================================

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAccount_InvalidOwnerName_Returns400(
        string ownerName)
    {
        var request = new RestRequest(
            "api/accounts",
            Method.Post);

        request.AddJsonBody(new
        {
            ownerName,
            initialBalance = 100m
        });

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest),
            $"Expected 400 for invalid owner name. " +
            $"Response: {response.Content}");
    }

    // =====================================================
    // TEST 11-12: CUSTOMER NAME LENGTH BOUNDARY TESTING
    // =====================================================

    [TestCase(100, 201)]
    [TestCase(101, 400)]
    public async Task CreateAccount_NameLengthBoundary_ReturnsExpectedStatus(
        int nameLength,
        int expectedStatus)
    {
        string ownerName = new string('A', nameLength);

        var request = new RestRequest(
            "api/accounts",
            Method.Post);

        request.AddJsonBody(new
        {
            ownerName,
            initialBalance = 100m
        });

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            (int)response.StatusCode,
            Is.EqualTo(expectedStatus),
            $"Name length: {nameLength}. " +
            $"Expected: {expectedStatus}. " +
            $"Actual: {(int)response.StatusCode}. " +
            $"Response: {response.Content}");

        if (expectedStatus == 201)
        {
            Assert.That(
                response.Content,
                Is.Not.Null.And.Not.Empty);

            var account = JsonSerializer.Deserialize<BankAccount>(
                response.Content!,
                JsonOptions);

            Assert.That(account, Is.Not.Null);

            Assert.That(
                account!.OwnerName.Length,
                Is.EqualTo(nameLength));
        }
    }

    // =====================================================
    // REUSABLE TEST DATA HELPER
    // =====================================================

    private async Task<BankAccount> CreateTestAccount()
    {
        string ownerName =
            $"QA Customer {Guid.NewGuid():N}";

        var request = new RestRequest(
            "api/accounts",
            Method.Post);

        request.AddJsonBody(new
        {
            ownerName,
            initialBalance = 500m
        });

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            $"Test setup failed: {response.Content}");

        Assert.That(
            response.Content,
            Is.Not.Null.And.Not.Empty);

        var account = JsonSerializer.Deserialize<BankAccount>(
            response.Content!,
            JsonOptions);

        Assert.That(account, Is.Not.Null);

        return account!;
    }

    // =====================================================
    // RESPONSE DATA MODEL
    // =====================================================

    private record BankAccount(
        Guid Id,
        string OwnerName,
        decimal Balance);
}
