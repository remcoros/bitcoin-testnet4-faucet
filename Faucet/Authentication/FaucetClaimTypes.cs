namespace Faucet.Authentication;

public static class FaucetClaimTypes
{
    public const string UserHash = "tn4-faucet:userhash";
    public const string AccountCreatedAt = "tn4-faucet:account_created_at";
    public static string IsAdmin { get; set; } = "tn4-faucet:role";
}