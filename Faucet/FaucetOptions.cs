using System.ComponentModel.DataAnnotations;
using Faucet.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Faucet;

public class FaucetOptions : IOptions<FaucetOptions>
{
    [Required] public required string ConnectionString { get; set; }
    [Required] public required string FaucetSecretSalt { get; set; }
    [Required] public required string RpcHost { get; set; }

    [Required] public required string RpcUsername { get; set; }

    [Required] public required string RpcPassword { get; set; }

    /// <summary>
    /// Used to find unspent utxos and as change address.
    /// </summary>
    [Required]
    [ValidBitcoinTestNetAddress]
    public required string FaucetAddress { get; set; }

    /// <summary>
    /// Private key (WIF format) for the faucet address.
    /// </summary>
    [Required]
    public required string FaucetPrivateKey { get; set; }

    /// <summary>
    /// Initial payout amount in satoshis.
    /// </summary>
    [Required]
    [Range(10_000, long.MaxValue)]
    public long InitialPayout { get; set; } = 10000000;

    /// <summary>
    /// Minimum payout amount in satoshis.
    /// </summary>
    [Required]
    [Range(10_000, long.MaxValue)]
    public long MinimumPayout { get; set; } = 1000000;

    /// <summary>
    /// Decay rate for exponential decay.
    /// </summary>
    [Required]
    [Range(0, 1)]
    public double DecayRate { get; set; } = 0.001;

    /// <summary>
    /// Optional OP_RETURN data to include in the faucet transactions.
    /// </summary>
    public string OpReturnData { get; set; } = string.Empty;

    /// <summary>
    /// When true, the faucet will not broadcast any transactions, and log the transaction hex instead.
    /// Default is true, so must be explicitly set to false.
    /// </summary>
    public bool TestMode { get; set; } = true;
    
    /// <summary>
    /// Admins can always request coins. See '/whoami' endpoint to get your user hash. Note that this changes if the
    /// secret salt is changed.
    /// </summary>
    public string? AdminUserHash { get; set; }
    
    FaucetOptions IOptions<FaucetOptions>.Value => this;
}