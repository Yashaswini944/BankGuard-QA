
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

namespace BankGuard.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    // Temporary in-memory storage for development.
    private static readonly ConcurrentDictionary<Guid, BankAccountDto>
        Accounts = new();

    // POST: api/accounts
    [HttpPost]
    public ActionResult<BankAccountDto> CreateAccount(
        [FromBody] CreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OwnerName) ||
            request.OwnerName.Trim().Length > 100 ||
            request.InitialBalance < 0)
        {
            return BadRequest(new
            {
                message = "Invalid account information."
            });
        }

        var account = new BankAccountDto(
            Guid.NewGuid(),
            request.OwnerName.Trim(),
            request.InitialBalance
        );

        Accounts[account.Id] = account;

        return CreatedAtAction(
            nameof(GetAccount),
            new { id = account.Id },
            account
        );
    }

    // GET: api/accounts/{id}
    [HttpGet("{id:guid}")]
    public ActionResult<BankAccountDto> GetAccount(Guid id)
    {
        if (!Accounts.TryGetValue(id, out var account))
        {
            return NotFound(new
            {
                message = "Account not found."
            });
        }

        return Ok(account);
    }
}

public record CreateAccountRequest(
    string OwnerName,
    decimal InitialBalance
);

public record BankAccountDto(
    Guid Id,
    string OwnerName,
    decimal Balance
);
