using Microsoft.EntityFrameworkCore;

namespace Faucet.Data;

public class FaucetDbContext(DbContextOptions<FaucetDbContext> options) : DbContext(options)
{
    public DbSet<TransactionHistoryEntry> TransactionHistory { get; set; }
}