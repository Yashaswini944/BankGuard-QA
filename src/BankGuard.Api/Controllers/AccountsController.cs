
using BankGuard.Api.Data;
using BankGuard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankGuard.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly BankGuardDbContext _context;

    // Inject database context
    public AccountsController(BankGuardDbContext context)
    {
        _context = context;
    }

    // ============================================
    // POST: api/accounts
    // Create a new bank account in SQL Server
    // ============================================

    [HttpPost]
    public async Task<ActionResult<BankAccountDto>> CreateAccount(
        [FromBody] CreateAccountRequest request)
    {
        // Validate account owner
        if (string.IsNullOrWhiteSpace(request.OwnerName))
        {
            return BadRequest(new
            {
                message = "Account owner name is required."
            });
        }

        string ownerName = request.OwnerName.Trim();

        if (ownerName.Length > 100)
        {
            return BadRequest(new
            {
                message = "Owner name cannot exceed 100 characters."
            });
        }

        // Validate initial balance
        if (request.InitialBalance < 0)
        {
            return BadRequest(new
            {
                message = "Initial balance cannot be negative."
            });
        }

        // Allow at most two decimal places
        if (decimal.Round(request.InitialBalance, 2)
            != request.InitialBalance)
        {
            return BadRequest(new
            {
                message = "Balance cannot have more than two decimal places."
            });
        }

        // Match SQL Server decimal(18,2) range
        if (request.InitialBalance > 9999999999999999.99m)
        {
            return BadRequest(new
            {
                message = "Initial balance exceeds the supported limit."
            });
        }

        // Create database entity
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            OwnerName = ownerName,
            Balance = request.InitialBalance,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Store account in SQL Server
        _context.Accounts.Add(account);

        await _context.SaveChangesAsync();

        // Convert entity to API response model
        var result = new BankAccountDto(
            account.Id,
            account.OwnerName,
            account.Balance
        );

        return CreatedAtAction(
            nameof(GetAccount),
            new { id = account.Id },
            result
        );
    }

    // ============================================
    // GET: api/accounts/{id}
    // Retrieve account from SQL Server
    // ============================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BankAccountDto>> GetAccount(
        Guid id)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null)
        {
            return NotFound(new
            {
                message = "Account not found."
            });
        }

        var result = new BankAccountDto(
            account.Id,
            account.OwnerName,
            account.Balance
        );

        return Ok(result);
    }
}

// ============================================
// REQUEST MODEL
// ============================================

public record CreateAccountRequest(
    string? OwnerName,
    decimal InitialBalance
);

// ============================================
// RESPONSE MODEL
// ============================================

public record BankAccountDto(
    Guid Id,
    string OwnerName,
    decimal Balance
);
