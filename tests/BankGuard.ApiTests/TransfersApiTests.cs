
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using RestSharp;

namespace BankGuard.ApiTests;

[TestFixture]
public class TransfersApiTests
{
    private RestClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // ============================================
    // TEST SETUP AND CLEANUP
    // ============================================

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

    // ============================================
    // TC-013: SUCCESSFUL BANKING TRANSFER
    // ============================================

    [Test]
    public async Task Transfer_ValidRequest_Returns201AndUpdatesBalances()
    {
        var source = await CreateTestAccountAsync(1000m);
        var destination = await CreateTestAccountAsync(500m);

        string key = Guid.NewGuid().ToString("N");

        var response = await PostTransferAsync(
            source.Id,
            destination.Id,
            250m,
            key);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            $"Response: {response.Content}; Error: {response.ErrorMessage}");

        var transfer = Deserialize<TransferDto>(response);

        Assert.Multiple(() =>
        {
            Assert.That(transfer.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(transfer.FromAccountId, Is.EqualTo(source.Id));
            Assert.That(transfer.ToAccountId, Is.EqualTo(destination.Id));
            Assert.That(transfer.Amount, Is.EqualTo(250m));
        });

        var sourceAfter = await GetAccountAsync(source.Id);
        var destinationAfter = await GetAccountAsync(destination.Id);

        Assert.Multiple(() =>
        {
            Assert.That(sourceAfter.Balance, Is.EqualTo(750m));
            Assert.That(destinationAfter.Balance, Is.EqualTo(750m));

            // Total funds must be preserved.
            Assert.That(
                sourceAfter.Balance + destinationAfter.Balance,
                Is.EqualTo(1500m));
        });
    }

    // ============================================
    // TC-014: INSUFFICIENT FUNDS
    // ============================================

    [Test]
    public async Task Transfer_InsufficientFunds_Returns400()
    {
        var source = await CreateTestAccountAsync(100m);
        var destination = await CreateTestAccountAsync(500m);

        var response = await PostTransferAsync(
            source.Id,
            destination.Id,
            200m,
            Guid.NewGuid().ToString("N"));

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest),
            $"Response: {response.Content}");

        var sourceAfter = await GetAccountAsync(source.Id);
        var destinationAfter = await GetAccountAsync(destination.Id);

        // No balances should change after rejection.
        Assert.Multiple(() =>
        {
            Assert.That(sourceAfter.Balance, Is.EqualTo(100m));
            Assert.That(destinationAfter.Balance, Is.EqualTo(500m));
        });
    }

    // ============================================
    // TC-015: NEGATIVE TRANSFER AMOUNT
    // ============================================

    [Test]
    public async Task Transfer_NegativeAmount_Returns400()
    {
        var source = await CreateTestAccountAsync(500m);
        var destination = await CreateTestAccountAsync(500m);

        var response = await PostTransferAsync(
            source.Id,
            destination.Id,
            -100m,
            Guid.NewGuid().ToString("N"));

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest),
            $"Response: {response.Content}");
    }

    // ============================================
    // TC-016: ZERO TRANSFER AMOUNT
    // ============================================

    [Test]
    public async Task Transfer_ZeroAmount_Returns400()
    {
        var source = await CreateTestAccountAsync(500m);
        var destination = await CreateTestAccountAsync(500m);

        var response = await PostTransferAsync(
            source.Id,
            destination.Id,
            0m,
            Guid.NewGuid().ToString("N"));

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest),
            $"Response: {response.Content}");
    }

    // ============================================
    // TC-017: SAME SOURCE AND DESTINATION
    // ============================================

    [Test]
    public async Task Transfer_SameAccount_Returns400()
    {
        var account = await CreateTestAccountAsync(500m);

        var response = await PostTransferAsync(
            account.Id,
            account.Id,
            100m,
            Guid.NewGuid().ToString("N"));

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest),
            $"Response: {response.Content}");
    }

    // ============================================
    // TC-018: SOURCE ACCOUNT NOT FOUND
    // ============================================

    [Test]
    public async Task Transfer_SourceAccountNotFound_Returns404()
    {
        var destination = await CreateTestAccountAsync(500m);

        var response = await PostTransferAsync(
            Guid.NewGuid(),
            destination.Id,
            100m,
            Guid.NewGuid().ToString("N"));

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound),
            $"Response: {response.Content}");
    }

    // ============================================
    // TC-019: DESTINATION ACCOUNT NOT FOUND
    // ============================================

    [Test]
    public async Task Transfer_DestinationAccountNotFound_Returns404()
    {
        var source = await CreateTestAccountAsync(500m);

        var response = await PostTransferAsync(
            source.Id,
            Guid.NewGuid(),
            100m,
            Guid.NewGuid().ToString("N"));

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound),
            $"Response: {response.Content}");
    }

    // ============================================
    // TC-020: DUPLICATE TRANSFER PREVENTION
    // ============================================

    [Test]
    public async Task Transfer_RepeatedRequest_DoesNotTransferTwice()
    {
        var source = await CreateTestAccountAsync(1000m);
        var destination = await CreateTestAccountAsync(500m);

        string key = Guid.NewGuid().ToString("N");

        var firstResponse = await PostTransferAsync(
            source.Id,
            destination.Id,
            250m,
            key);

        Assert.That(
            firstResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.Created));

        var firstTransfer = Deserialize<TransferDto>(firstResponse);

        // Repeat exactly the same transfer.
        var secondResponse = await PostTransferAsync(
            source.Id,
            destination.Id,
            250m,
            key);

        Assert.That(
            secondResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            $"Response: {secondResponse.Content}");

        var secondTransfer = Deserialize<TransferDto>(secondResponse);

        var sourceAfter = await GetAccountAsync(source.Id);
        var destinationAfter = await GetAccountAsync(destination.Id);

        Assert.Multiple(() =>
        {
            // Both responses must identify the same transaction.
            Assert.That(
                secondTransfer.Id,
                Is.EqualTo(firstTransfer.Id));

            // Money must move only once.
            Assert.That(sourceAfter.Balance, Is.EqualTo(750m));
            Assert.That(destinationAfter.Balance, Is.EqualTo(750m));
        });
    }

    // ============================================
    // TC-021: IDEMPOTENCY KEY CONFLICT
    // ============================================

    [Test]
    public async Task Transfer_SameKeyDifferentAmount_Returns409()
    {
        var source = await CreateTestAccountAsync(500m);
        var destination = await CreateTestAccountAsync(500m);

        string key = Guid.NewGuid().ToString("N");

        var firstResponse = await PostTransferAsync(
            source.Id,
            destination.Id,
            100m,
            key);

        Assert.That(
            firstResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.Created));

        // Same key, different transfer amount.
        var secondResponse = await PostTransferAsync(
            source.Id,
            destination.Id,
            150m,
            key);

        Assert.That(
            secondResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.Conflict),
            $"Response: {secondResponse.Content}");

        var sourceAfter = await GetAccountAsync(source.Id);
        var destinationAfter = await GetAccountAsync(destination.Id);

        Assert.Multiple(() =>
        {
            Assert.That(sourceAfter.Balance, Is.EqualTo(400m));
            Assert.That(destinationAfter.Balance, Is.EqualTo(600m));
        });
    }

    // ============================================
    // TC-022: RETRIEVE COMPLETED TRANSFER
    // ============================================

    [Test]
    public async Task GetTransfer_ExistingId_Returns200()
    {
        var source = await CreateTestAccountAsync(500m);
        var destination = await CreateTestAccountAsync(500m);

        var createResponse = await PostTransferAsync(
            source.Id,
            destination.Id,
            100m,
            Guid.NewGuid().ToString("N"));

        Assert.That(
            createResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.Created));

        var createdTransfer = Deserialize<TransferDto>(createResponse);

        var getRequest = new RestRequest(
            $"api/transfers/{createdTransfer.Id}",
            Method.Get);

        var getResponse = await _client.ExecuteAsync(getRequest);

        Assert.That(
            getResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            $"Response: {getResponse.Content}");

        var retrievedTransfer = Deserialize<TransferDto>(getResponse);

        Assert.Multiple(() =>
        {
            Assert.That(
                retrievedTransfer.Id,
                Is.EqualTo(createdTransfer.Id));

            Assert.That(
                retrievedTransfer.FromAccountId,
                Is.EqualTo(source.Id));

            Assert.That(
                retrievedTransfer.ToAccountId,
                Is.EqualTo(destination.Id));

            Assert.That(
                retrievedTransfer.Amount,
                Is.EqualTo(100m));
        });
    }

    // ============================================
    // REUSABLE HELPER: CREATE TEST ACCOUNT
    // ============================================

    private async Task<AccountDto> CreateTestAccountAsync(
        decimal initialBalance)
    {
        string ownerName = $"Transfer QA {Guid.NewGuid():N}";

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
            $"Account setup failed: {response.Content}; " +
            $"Error: {response.ErrorMessage}");

        return Deserialize<AccountDto>(response);
    }

    // ============================================
    // REUSABLE HELPER: GET ACCOUNT BALANCE
    // ============================================

    private async Task<AccountDto> GetAccountAsync(Guid accountId)
    {
        var request = new RestRequest(
            $"api/accounts/{accountId}",
            Method.Get);

        var response = await _client.ExecuteAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            $"Account retrieval failed: {response.Content}");

        return Deserialize<AccountDto>(response);
    }

    // ============================================
    // REUSABLE HELPER: POST TRANSFER
    // ============================================

    private async Task<RestResponse> PostTransferAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string idempotencyKey)
    {
        var request = new RestRequest(
            "api/transfers",
            Method.Post);

        request.AddJsonBody(new
        {
            fromAccountId,
            toAccountId,
            amount,
            idempotencyKey
        });

        return await _client.ExecuteAsync(request);
    }

    // ============================================
    // REUSABLE HELPER: PARSE JSON RESPONSE
    // ============================================

    private static T Deserialize<T>(RestResponse response)
        where T : class
    {
        Assert.That(
            response.Content,
            Is.Not.Null.And.Not.Empty);

        var result = JsonSerializer.Deserialize<T>(
            response.Content!,
            JsonOptions);

        Assert.That(result, Is.Not.Null);

        return result!;
    }

    // ============================================
    // RESPONSE DATA MODELS
    // ============================================

    private record AccountDto(
        Guid Id,
        string OwnerName,
        decimal Balance);

    private record TransferDto(
        Guid Id,
        Guid FromAccountId,
        Guid ToAccountId,
        decimal Amount,
        DateTime CompletedAtUtc);
}
