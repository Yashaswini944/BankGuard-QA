
using System.Data;
using BankGuard.Api.Data;
using BankGuard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BankGuard.Api.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private const decimal MaxBalance = 9999999999999999.99m;
    private const int MaxDeadlockAttempts = 4;

    private readonly BankGuardDbContext _context;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(
        BankGuardDbContext context,
        ILogger<TransfersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =====================================================
    // POST: api/transfers
    // Create a banking transfer with transaction protection
    // =====================================================

    [HttpPost]
    public async Task<ActionResult<BankTransferDto>> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        // =================================================
        // 1. VALIDATE ACCOUNT IDs
        // =================================================

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

        // =================================================
        // 2. VALIDATE TRANSFER AMOUNT
        // =================================================

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

        // =================================================
        // 3. VALIDATE IDEMPOTENCY KEY
        // =================================================

        string key = request.IdempotencyKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
        {
            return BadRequest(new
            {
                message = "A valid idempotency key is required (maximum 128 characters)."
            });
        }

        // =================================================
        // 4. RETRY COMPLETE TRANSACTION ON SQL DEADLOCK
        // =================================================

        for (int attempt = 1; attempt <= MaxDeadlockAttempts; attempt++)
        {
            // Ensure a retry does not reuse stale tracked entities.
            _context.ChangeTracker.Clear();

            try
            {
                // Serializable isolation prevents competing
                // transactions from reading inconsistent balances.
                await using var dbTransaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                // =============================================
                // 5. CHECK IDEMPOTENCY
                // =============================================

                var existingTransfer = await _context.Transfers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.IdempotencyKey == key,
                        cancellationToken);

                if (existingTransfer != null)
                {
                    // A previously used key cannot represent
                    // a different transfer.
                    if (existingTransfer.FromAccountId != request.FromAccountId ||
                        existingTransfer.ToAccountId != request.ToAccountId ||
                        existingTransfer.Amount != request.Amount)
                    {
                        return Conflict(new
                        {
                            message =
                                "Idempotency key was already used for a different transfer."
                        });
                    }

                    // Same request: return original transfer.
                    // No balances are changed.
                    return Ok(ToDto(existingTransfer));
                }

                // =============================================
                // 6. LOAD SOURCE ACCOUNT
                // =============================================

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

                // =============================================
                // 7. LOAD DESTINATION ACCOUNT
                // =============================================

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

                // =============================================
                // 8. VALIDATE CURRENT BALANCES
                // =============================================

                // Important: these checks execute again on
                // each retry using freshly loaded balances.

                if (source.Balance < request.Amount)
                {
                    return BadRequest(new
                    {
                        message = "Insufficient funds."
                    });
                }

                if (destination.Balance > MaxBalance - request.Amount)
                {
                    return BadRequest(new
                    {
                        message =
                            "Destination balance would exceed the supported limit."
                    });
                }

                // =============================================
                // 9. UPDATE ACCOUNT BALANCES
                // =============================================

                source.Balance -= request.Amount;
                destination.Balance += request.Amount;

                // =============================================
                // 10. CREATE TRANSFER RECORD
                // =============================================

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

                // =============================================
                // 11. SAVE AND COMMIT ATOMICALLY
                // =============================================

                await _context.SaveChangesAsync(
                    cancellationToken);

                await dbTransaction.CommitAsync(
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetTransfer),
                    new { id = transfer.Id },
                    ToDto(transfer));
            }
            catch (Exception ex) when (
                IsSqlDeadlock(ex) &&
                !cancellationToken.IsCancellationRequested)
            {
                // SQL Server error 1205 means this transaction
                // was selected as the deadlock victim.
                //
                // The transaction is disposed before reaching
                // this handler. The next attempt starts a new
                // transaction and reloads account balances.

                _logger.LogWarning(
                    ex,
                    "SQL Server deadlock during banking transfer. " +
                    "Attempt {Attempt} of {Maximum}.",
                    attempt,
                    MaxDeadlockAttempts);

                if (attempt == MaxDeadlockAttempts)
                {
                    // Do not pretend a database failure is
                    // an insufficient-funds validation error.
                    return StatusCode(503, new
                    {
                        message =
                            "Transfer temporarily unavailable due to concurrent database activity. " +
                            "Please retry using the same idempotency key."
                    });
                }

                // Small increasing delay and jitter reduce
                // repeated collisions between requests.
                int delayMilliseconds =
                    (attempt * 100) + Random.Shared.Next(25, 101);

                await Task.Delay(
                    delayMilliseconds,
                    cancellationToken);
            }
        }

        return StatusCode(503, new
        {
            message = "Transfer temporarily unavailable."
        });
    }

    // =====================================================
    // GET: api/transfers/{id}
    // Retrieve an existing transfer
    // =====================================================

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

    // =====================================================
    // HELPER: DETECT SQL SERVER DEADLOCK ERROR 1205
    // =====================================================

    private static bool IsSqlDeadlock(Exception exception)
    {
        // EF Core can wrap SqlException inside DbUpdateException
        // or InvalidOperationException. Inspect inner exceptions.

        Exception? current = exception;

        while (current != null)
        {
            if (current is SqlException sqlException &&
                sqlException.Number == 1205)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    // =====================================================
    // HELPER: CONVERT DATABASE ENTITY TO RESPONSE DTO
    // =====================================================

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

// =====================================================
// RESPONSE MODEL
// =====================================================

public record BankTransferDto(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTime CompletedAtUtc);
