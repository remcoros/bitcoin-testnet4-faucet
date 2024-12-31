using System.ComponentModel.DataAnnotations;
using Faucet.Authentication;
using Faucet.Data;
using Faucet.DataAnnotations;
using Faucet.Wallet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NBitcoin;

namespace Faucet.WebApp.Pages;

public class IndexModel(ILogger<IndexModel> log, FaucetServices faucetServices, FaucetWallet wallet, FaucetDbContext dbContext) : PageModel
{
    [Required(ErrorMessage = "Receiving address is required.")]
    [ValidBitcoinTestNetAddress]
    [BindProperty]
    public string ReceivingAddress { get; set; } = string.Empty;
    public Money WalletBalance { get; set; } = Money.Satoshis(0);
    public bool IsUserAuthenticated { get; set; }
    public bool IsUserEligible { get; set; }
    public string? NotEligibleReason { get; set; }
    public bool PayoutSuccessful { get; set; }
    public string? TransactionId { get; set; }
    public List<TransactionHistoryEntry> TransactionHistory { get; set; } = new();


    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        if (User.Identity?.IsAuthenticated == true)
        {
            // Get past transactions of this user
            var userHash = User.FindFirst(FaucetClaimTypes.UserHash)!.Value;
            var transactionHistory = await dbContext.TransactionHistory
                .Where(i => i.UserHash == userHash)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
            TransactionHistory = transactionHistory;
        }

        if (TempData.TryGetValue("PayoutSuccessful", out var payoutSuccessful) && payoutSuccessful is true)
        {
            PayoutSuccessful = true;
            TransactionId = TempData["TransactionId"] as string ?? "unknown";
        }
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Cannot POST if not authenticated
        if (User.Identity?.IsAuthenticated is not true)
        {
            return RedirectToPage();
        }
        
        // Initialize some properties
        await InitializeAsync(cancellationToken);
        
        // Check if user is still eligible (this is set in InitializeAsync)
        if (!IsUserEligible)
        {
            return RedirectToPage();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validation
        BitcoinAddress receivingAddress;
        try
        {
            receivingAddress = BitcoinAddress.Create(ReceivingAddress, Bitcoin.Instance.Testnet4);
        }
        catch (Exception)
        {
            ModelState.AddModelError(nameof(ReceivingAddress), "Invalid receiving address.");
            return Page();
        }

        // Send coins
        try
        {
            var transactionId = await faucetServices.SendCoinsAsync(User, receivingAddress.ToString(), cancellationToken);
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                throw new InvalidOperationException("no transaction id received");
            }
            
            // return to the page to show a success message
            TempData["PayoutSuccessful"] = true;
            TempData["TransactionId"] = transactionId;
            
            return RedirectToPage();
        }
        catch(Exception e)
        {
            log.LogError(e, "Failed to send coins");
            ModelState.AddModelError(nameof(ReceivingAddress), "Something went wrong. Please try again later.");
            return Page();
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            IsUserAuthenticated = true;

            // Check eligibility
            var (eligible, reason) = await faucetServices.UserIsEligibleAsync(User);
            
            IsUserEligible = eligible;
            NotEligibleReason = reason;
        }

        WalletBalance = await wallet.GetBalanceAsync(cancellationToken);
    }
}