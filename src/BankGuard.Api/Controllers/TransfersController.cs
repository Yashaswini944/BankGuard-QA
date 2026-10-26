
using System.Data;
using BankGuard.Api.Data;
using BankGuard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankGuard.Api.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private const decimal MaxBalance = 9999999999999999.99m;

    private readonly BankGuardDbContext _context;

    public TransfersController(BankGuardDbContext context)
    {
        _context = context;
    }

    // POST: api/transfers
    [HttpPost]
    public async Task<ActionResult<BankTransferDto>> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        // Validate account IDs.
        if (request.FromAccountId == Guid.Empty ||
            request.ToAccountId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Valid source and destination account IDs are required."
            });
        }

        if (request.FromAccountId == request.ToAccountId)
        {
            return BadRequest(new
            {
                message = "Source and destination accounts must be different."
            });
        }

        // Validate transfer amount.
        if (request.Amount <= 0)
        {
            return BadRequest(new
            {
                message = "Transfer amount must be greater than zero."
            });
        }

        if (decimal.Round(request.Amount, 2) != request.Amount)
        {
            return BadRequest(new
            {
                message = "Transfer amount cannot have more than two decimal places."
            });
        }

        if (request.Amount > MaxBalance)
        {
            return BadRequest(new
            {
                message = "Transfer amount exceeds the supported limit."
            });
        }

        // Validate idempotency key.
        string key = request.IdempotencyKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
        {
            return BadRequest(new
            {
                message = "A valid idempotency key is required (maximum 128 characters)."
            });
        }

        // Keep the debit, credit, and transfer record in
        // a single SQL Server database transaction.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Check whether this request was already completed.
        var existingTransfer = await _context.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.IdempotencyKey == key,
                cancellationToken);

        if (existingTransfer != null)
        {
            // The same key must not represent different transfers.
            if (existingTransfer.FromAccountId != request.FromAccountId ||
                existingTransfer.ToAccountId != request.ToAccountId ||
                existingTransfer.Amount != request.Amount)
            {
                return Conflict(new
                {
                    message = "Idempotency key was already used for a different transfer."
                });
            }

            return Ok(ToDto(existingTransfer));
        }

        // Retrieve the sending account.
        var source = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == request.FromAccountId,
                cancellationToken);

        if (source == null)
        {
            return NotFound(new
            {
                message = "Source account not found."
            });
        }

        // Retrieve the receiving account.
        var destination = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == request.ToAccountId,
                cancellationToken);

        if (destination == null)
        {
            return NotFound(new
            {
                message = "Destination account not found."
            });
        }

        // Check available funds.
        if (source.Balance < request.Amount)
        {
            return BadRequest(new
            {
                message = "Insufficient funds."
            });
        }

        // Prevent overflow of SQL Server decimal(18,2).
        if (destination.Balance > MaxBalance - request.Amount)
        {
            return BadRequest(new
            {
                message = "Destination balance would exceed the supported limit."
            });
        }

        // Debit and credit the accounts.
        source.Balance -= request.Amount;
        destination.Balance += request.Amount;

        // Record the completed transfer.
        var transfer = new BankTransaction
        {
            Id = Guid.NewGuid(),
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            Amount = request.Amount,
            IdempotencyKey = key,
            CompletedAtUtc = DateTime.UtcNow
        };

        _context.Transfers.Add(transfer);

        // Persist both balance changes and the transfer record.
        await _context.SaveChangesAsync(cancellationToken);

        // Commit all changes together.
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetTransfer),
            new { id = transfer.Id },
            ToDto(transfer));
    }

    // GET: api/transfers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BankTransferDto>> GetTransfer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var transfer = await _context.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);

        if (transfer == null)
        {
            return NotFound(new
            {
                message = "Transfer not found."
            });
        }

        return Ok(ToDto(transfer));
    }

    private static BankTransferDto ToDto(BankTransaction transfer)
    {
        return new BankTransferDto(
            transfer.Id,
            transfer.FromAccountId,
            transfer.ToAccountId,
            transfer.Amount,
            transfer.CompletedAtUtc);
    }
}

// API response model.
public record BankTransferDto(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTime CompletedAtUtc);
