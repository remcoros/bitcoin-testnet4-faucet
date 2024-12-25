using System.Globalization;
using System.Security.Claims;
using Faucet.Authentication;
using Faucet.Data;
using Faucet.Utilities;
using Faucet.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace Faucet;

public class FaucetServices
{
    private static readonly SemaphoreSlim s_requestTrackerLock = new(1, 1);
    private readonly FaucetDbContext _dbContext;
    private readonly FaucetWallet _wallet;
    private readonly FaucetOptions _options;
    private readonly FaucetPayoutCalculator _payoutCalculator;

    public FaucetServices(IOptions<FaucetOptions> options, FaucetDbContext dbContext, FaucetWallet wallet)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _wallet = wallet;
        _payoutCalculator =
            new FaucetPayoutCalculator(_options.InitialPayout, _options.MinimumPayout, _options.DecayRate);
    }

    public string GenerateUserHash(string provider, string userId)
    {
        return FaucetUserHashGenerator.GenerateHash(_options.FaucetSecretSalt, provider, userId);
    }

    public bool IsAdminUser(ClaimsPrincipal user)
    {
        var userHash = user.FindFirst(FaucetClaimTypes.UserHash)?.Value;
        return IsAdminUser(userHash);
    }
    
    private bool IsAdminUser(string? userHash)
    {
        if (!string.IsNullOrWhiteSpace(_options.AdminUserHash) && _options.AdminUserHash == userHash)
        {
            return true;
        }

        return false;
    }
    
    public async Task<(bool Eligible, string Reason)> UserIsEligibleAsync(ClaimsPrincipal user)
    {
        var userHash = user.FindFirst(FaucetClaimTypes.UserHash)?.Value;
        if (string.IsNullOrEmpty(userHash))
        {
            return (false, "User hash claim not found");
        }

        // admins are always eligible
        if (IsAdminUser(userHash))
        {
            return (true, "");
        }

        var createdAt = user.FindFirst(FaucetClaimTypes.AccountCreatedAt)?.Value;
        if (string.IsNullOrEmpty(userHash) 
            || !DateTimeOffset.TryParse(createdAt, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var createdAtDate))
        {
            return (false, "User account creation date claim not found");
        }

        if (createdAtDate > DateTime.UtcNow.AddMonths(-6))
        {
            return (false, "User account is too new");
        }

        var receivedCoins = await UserHasReceivedCoinsAsync(userHash);
        if (receivedCoins)
        {
            return (false, "User account already received coins");
        }

        return (true, "");
    }

    public async Task<string> SendCoinsAsync(ClaimsPrincipal user, string receivingAddress, CancellationToken cancellationToken = default)
    {
        // Check if user is eligible
        var (eligible, reason) = await UserIsEligibleAsync(user);
        if (!eligible)
        {
            throw new InvalidOperationException($"User is not eligible: {reason}");
        }

        var userHash = user.FindFirst(FaucetClaimTypes.UserHash)!.Value;

        // global lock until the transaction is finished so we have an accurate request count  
        await s_requestTrackerLock.WaitAsync(cancellationToken);
        
        try
        {
            var requestCount = await _dbContext.TransactionHistory.CountAsync(cancellationToken) + 1;
            var amountToSend = Money.Satoshis(_payoutCalculator.CalculatePayout(requestCount));
        
            // Add a dummy record to the history so that we don't send coins to the same user again while the transaction
            // is being sent by the wallet
            var historyEntry = new TransactionHistoryEntry(userHash, "pending", amountToSend.Satoshi);
            _dbContext.TransactionHistory.Add(historyEntry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var transactionId = await _wallet.SendAmountAsync(receivingAddress, amountToSend, cancellationToken);
                historyEntry.TransactionId = transactionId;
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            
                return transactionId;
            }
            catch(Exception)
            {
                // remove the dummy record
                _dbContext.TransactionHistory.Remove(historyEntry);
                await _dbContext.SaveChangesAsync(cancellationToken);
            
                throw;
            }
        }
        finally
        {
            s_requestTrackerLock.Release();
        }
    }
    
    private async Task<bool> UserHasReceivedCoinsAsync(string userHash)
    {
        return await _dbContext.TransactionHistory.AnyAsync(i => i.UserHash == userHash);
    }
}