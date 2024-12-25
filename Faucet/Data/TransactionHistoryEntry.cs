using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Faucet.Data;

[Index(nameof(UserHash))]
public class TransactionHistoryEntry
{
    [Key] 
    public int Id { get; private set; }

    [Required]
    [MaxLength(64)]
    public string UserHash { get; set; }
    
    [Required]
    [MaxLength(64)]
    public string TransactionId { get; set; }
    
    [Required]
    public long Amount { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TransactionHistoryEntry(string userHash, string transactionId, long amount)
    {
        UserHash = userHash;
        TransactionId = transactionId;
        Amount = amount;
    }
}