using System.ComponentModel.DataAnnotations;
using NBitcoin;

namespace Faucet.DataAnnotations;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class ValidBitcoinTestNetAddressAttribute() : ValidationAttribute(() => "Invalid Bitcoin testnet address")
{
    public override bool IsValid(object? value)
    {
        if (value is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        try
        {
            BitcoinAddress.Create(stringValue, Bitcoin.Instance.Testnet4);
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
}