
using System;
using System.ComponentModel.DataAnnotations;

namespace BankGuard.Api.Models;

public sealed class CreateTransferRequest
{
    // Account sending the money
    public Guid FromAccountId { get; set; }

    // Account receiving the money
    public Guid ToAccountId { get; set; }

    // Amount to transfer
    public decimal Amount { get; set; }

    // Client-provided key to identify repeated requests
    [Required]
    [MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
