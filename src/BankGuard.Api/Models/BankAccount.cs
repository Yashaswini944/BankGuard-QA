
using System;

namespace BankGuard.Api.Models;

public class BankAccount
{
    // Unique identifier for the bank account
    public Guid Id { get; set; } = Guid.NewGuid();

    // Name of the fictional account owner
    public string OwnerName { get; set; } = string.Empty;

    // Current account balance
    public decimal Balance { get; set; }

    // Date and time the account was created (UTC)
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
