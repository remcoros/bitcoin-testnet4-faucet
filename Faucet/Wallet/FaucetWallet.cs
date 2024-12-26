﻿using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;

namespace Faucet.Wallet;

public class FaucetWallet
{
    private const string WalletBalanceCacheKey = nameof(WalletBalanceCacheKey);
    
    private readonly Network _network = Bitcoin.Instance.Testnet4;
    private readonly FaucetOptions _options;
    private readonly BitcoinAddress _faucetAddress;
    private readonly SemaphoreSlim _walletLock = new(1, 1);
    private readonly RPCClient _rpcClient;
    private readonly byte[]? _opReturnData;
    private readonly ILogger<FaucetWallet> _log;
    private readonly IMemoryCache _memoryCache;
    
    public FeeRate FeeRate { get; } = new(1m);

    public FaucetWallet(ILogger<FaucetWallet> log, IOptions<FaucetOptions> options, IMemoryCache memoryCache)
    {
        _log = log;
        _memoryCache = memoryCache;
        _options = options.Value;
        _rpcClient = CreateRpcClient();
        
        try
        {
            _faucetAddress = BitcoinAddress.Create(_options.FaucetAddress, _network);
        }
        catch(Exception e)
        {
            throw new ArgumentException("Invalid FaucetAddress specified in options", e);
        }
        
        if (!string.IsNullOrEmpty(_options.OpReturnData))
        {
            _opReturnData = Encoding.UTF8.GetBytes(_options.OpReturnData).ToArray();
        }
    }
    
    private RPCClient CreateRpcClient()
    {
        var credentials = new NetworkCredential(_options.RpcUsername, _options.RpcPassword);
        var rpcClient = new RPCClient(credentials, new Uri(_options.RpcHost), _network)
        {
            HttpClient = new HttpClient(new HttpClientHandler()
            {
                // ignore ssl certificate errors
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            })
        };
        
        return rpcClient;
    }

    public async Task<string> SendAmountAsync(string receivingAddress, Money amount, CancellationToken cancellationToken = default)
    {
        // lock the wallet
        await _walletLock.WaitAsync(cancellationToken);
        
        try
        {
            _log.LogInformation("Sending {Amount} to {ReceivingAddress}", amount.ToUnit(MoneyUnit.BTC), receivingAddress);
            
            var transaction = await BuildTransactionAsync(receivingAddress, amount, cancellationToken);
            var transactionId = await BroadcastTransactionAsync(transaction, cancellationToken);

            _memoryCache.Remove(WalletBalanceCacheKey);
            
            return transactionId;
        }
        finally
        {
            _walletLock.Release();
        }
    }

    private async Task<string> BroadcastTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var txId = transaction.GetHash().ToString();
        _log.LogInformation("Broadcasting transaction {TransactionId}", txId);
        
        if (_options.TestMode)
        {
            _log.LogInformation("Test mode enabled, not broadcasting transaction:\n{Transaction}", transaction);
            return txId;
        }

        var rpcTxId = (await _rpcClient.SendRawTransactionAsync(transaction, cancellationToken)).ToString();
        _log.LogInformation("Broadcasted transaction {TransactionId}", rpcTxId);
        
        return txId;
    }

    public async Task<Transaction> BuildTransactionAsync(string receivingAddress, Money amount, CancellationToken cancellationToken)
    {
        // Validate receiving address
        BitcoinAddress receivingBitcoinAddress;
        try
        {
            receivingBitcoinAddress = BitcoinAddress.Create(receivingAddress, _network);
        }
        catch(Exception e)
        {
            throw new ArgumentException("Invalid receiving address", nameof(receivingAddress), e);
        }
            
        var unspentCoins = await GetUnspentCoinsAsync(cancellationToken);
        
        var privateKey = Key.Parse(_options.FaucetPrivateKey, _network);

        var transaction = _network.CreateTransactionBuilder();
        transaction.DustPrevention = false;
        transaction
            .AddKeys(privateKey)
            .SetCoinSelector(new DefaultCoinSelector()
            {
                GroupByScriptPubKey = false
            })
            .AddCoins(unspentCoins.Select(coin => coin.AsCoin()))
            .Send(receivingBitcoinAddress, amount)
            // Subtract fees from the amount sent
            .SubtractFees()
            .SetChange(_faucetAddress)
            .SendEstimatedFees(FeeRate);

        if (_opReturnData is not null)
        {
            transaction.Send(TxNullDataTemplate.Instance.GenerateScriptPubKey(_opReturnData), Money.Zero);
        }

        return transaction.BuildTransaction(true);
    }

    public async Task<List<UnspentCoin>> GetUnspentCoinsAsync(CancellationToken cancellationToken = default)
    {
        var unspentCoins = await _rpcClient.ListUnspentAsync(0, int.MaxValue, _faucetAddress)
            .WithCancellation(cancellationToken);

        return unspentCoins.OrderByDescending(x => x.Confirmations).ToList();
    }

    public async Task<Money> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        return await _memoryCache.GetOrCreateAsync(WalletBalanceCacheKey, async entry =>
        {
            entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            var unspent = await GetUnspentCoinsAsync(cancellationToken);
            return unspent.Sum(x => x.Amount);
        });
    }
}