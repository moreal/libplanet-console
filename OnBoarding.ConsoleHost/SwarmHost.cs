using System.Net;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Net.Consensus;
using Libplanet.Blockchain;
using System.Collections.Immutable;
using Libplanet.Action;
using Libplanet.Types.Tx;

namespace OnBoarding.ConsoleHost;

sealed class SwarmHost : IAsyncDisposable
{
    public static readonly PrivateKey AppProtocolKey = PrivateKey.FromString
    (
        "2a15e7deaac09ce631e1faa184efadb175b6b90989cf1faed9dfc321ad1db5ac"
    );

    private readonly User _user;
    private readonly Swarm _swarm;
    private Task? _startTask;
    private bool _isDisposed;

    public SwarmHost(User user, UserCollection users)
    {
        _user = user;
        _swarm = Create(user, users);
    }

    public string Key => $"{_user.PublicKey}";

    public bool IsRunning => _startTask != null;

    public bool IsDisposed => _isDisposed;

    public Swarm Target => _swarm;

    public BlockChain BlockChain => _swarm.BlockChain;

    public override string ToString()
    {
        return $"{_swarm.EndPoint.Host}:{_swarm.EndPoint.Port}";
    }

    public void StageTransaction(User user, IAction[] actions)
    {
        var blockChain = BlockChain;
        var privateKey = user.PrivateKey;
        var genesisBlock = blockChain.Genesis;
        var nonce = blockChain.GetNextTxNonce(privateKey.ToAddress());
        var values = actions.Select(item => item.PlainValue).ToArray();
        var transaction = Transaction.Create(
            nonce: nonce,
            privateKey: privateKey,
            genesisHash: genesisBlock.Hash,
            actions: new TxActionList(values)
        );
        blockChain.StageTransaction(transaction);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed == true)
            throw new ObjectDisposedException($"{this}");
        if (_startTask != null)
            throw new InvalidOperationException("Swarm has been started.");

        // await _swarm.BootstrapAsync(default);
        _startTask = _swarm.StartAsync(cancellationToken: default);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed == true)
            throw new ObjectDisposedException($"{this}");
        if (_startTask == null)
            throw new InvalidOperationException("Swarm has been stopped.");

        await _swarm.StopAsync(cancellationToken: cancellationToken);
        await _startTask;
        _swarm.Dispose();
        _startTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed == true)
            throw new ObjectDisposedException($"{this}");

        if (_startTask != null)
        {
            await _swarm.StopAsync(cancellationToken: default);
            await _startTask;
            _swarm.Dispose();
            _startTask = null;
        }

        _isDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Disposed;

    private static Swarm Create(User user, UserCollection users)
    {
        var validatorKeys = users.Select(item => item.PublicKey).ToArray();
        var index = users.IndexOf(user);
        var seedUser = users.Where(item => user.Peer != item.Peer).ToArray()[new Random().Next(users.Count - 1)];
        var consensusPeers = users.Select(item => item.ConsensusPeer).ToArray();
        var blockChain = BlockChainUtils.CreateBlockChain(user.Name, validatorKeys);
        var privateKey = user.PrivateKey;
        var transport = CreateTransport(privateKey, user.Peer.EndPoint.Port);
        var swarmOptions = new SwarmOptions
        {
            StaticPeers = ImmutableHashSet.Create(seedUser.Peer),
        };
        var consensusTransport = CreateTransport(privateKey, user.ConsensusPeer.EndPoint.Port);
        var consensusReactorOption = new ConsensusReactorOption
        {
            SeedPeers = ImmutableList.Create(seedUser.ConsensusPeer),
            ConsensusPeers = ImmutableList.Create(consensusPeers),
            ConsensusPort = user.ConsensusPeer.EndPoint.Port,
            ConsensusPrivateKey = privateKey,
            // ConsensusWorkers = 100,
            TargetBlockInterval = TimeSpan.FromSeconds(10),
            ContextTimeoutOptions = new(),
        };
        return new Swarm(blockChain, privateKey, transport, swarmOptions, consensusTransport, consensusReactorOption);
    }

    private static NetMQTransport CreateTransport(PrivateKey privateKey, int port)
    {
        var apv = AppProtocolVersion.Sign(AppProtocolKey, 1);
        var appProtocolVersionOptions = new AppProtocolVersionOptions
        {
            AppProtocolVersion = apv,
        };
        var hostOptions = new HostOptions($"{IPAddress.Loopback}", Array.Empty<IceServer>(), port);
        var task = NetMQTransport.Create(privateKey, appProtocolVersionOptions, hostOptions);
        task.Wait();
        return task.Result;
    }
}
