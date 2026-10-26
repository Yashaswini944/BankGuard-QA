
using System;

namespace BankGuard.Api.Models;

public class BankTransaction
{
    // Unique identifier for a completed transfer
    public Guid Id { get; set; } = Guid.NewGuid();

    // Account sending money
    public Guid FromAccountId { get; set; }

    // Account receiving money
    public Guid ToAccountId { get; set; }

    // Amount transferred
    public decimal Amount { get; set; }

    // Key used to prevent duplicate transfer processing
    public string IdempotencyKey { get; set; } = string.Empty;

    // Time the transfer was completed
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
}
