// NexusUnleashed - clean-room authored. The server host entry point. Boots the
// STS login server and the world GameServer from config and runs until
// stopped. Systems attach to this host as they are built and pinned against
// the oracle.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Network;
using NexusUnleashed.Sts;

namespace NexusUnleashed.Realm;

internal static class Program
{
    private static async Task Main()
    {
        RealmConfig cfg = RealmConfig.Load("realm.json");
        Log.Info($"=== {cfg.RealmName} realm host starting ===");
        Log.Info($"MotD: {cfg.MessageOfTheDay}");
        Log.Info($"bind {cfg.BindAddress} | sts {cfg.StsPort} | auth {cfg.AuthPort} | world {cfg.WorldPort}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // STS login server - flow pinned from the client, bodies UNPINNED.
        var sts = new StsServer(cfg.BindAddress, cfg.StsPort);
        IAccountStore accounts = string.IsNullOrWhiteSpace(cfg.AuthDatabase)
            ? new InMemoryAccountStore()
            : new NexusUnleashed.Database.DbAccountStore(cfg.AuthDatabase);
        AuthFlow.Register(sts, accounts);
        Log.Info($"sts login server listening ({accounts.GetType().Name}; body schemas pending oracle capture).");

        // World game server - encrypted packed-container channel (0x03DC/0x0244,
        // static-seeded PacketCrypt). The handshake sends the 0x0003 hello on
        // connect and routes the client's login messages toward world entry.
        var world = new GameServer(cfg.BindAddress, cfg.WorldPort, worldChannel: true);
        WorldHandshake.Register(world);
        Log.Info("world server listening (encrypted channel; 0x0003 hello on connect).");

        try
        {
            await Task.WhenAll(sts.ListenAsync(cts.Token), world.ListenAsync(cts.Token));
        }
        catch (OperationCanceledException)
        {
            Log.Info("shutdown requested; realm host stopping.");
        }
    }
}

/// <summary>
/// Development account store: accounts live in memory until the DB layer
/// lands. SRP credentials are generated on first sight so the flow can be
/// exercised end to end without a database.
/// </summary>
internal sealed class InMemoryAccountStore : IAccountStore
{
    private readonly ConcurrentDictionary<string, (byte[] Salt, byte[] Verifier)> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Guid> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public Task<(byte[] Salt, byte[] Verifier)?> GetSrpCredentialsAsync(string loginName)
    {
        if (string.IsNullOrWhiteSpace(loginName))
            return Task.FromResult<(byte[], byte[])?>(null);
        var creds = _accounts.GetOrAdd(loginName, _ =>
        {
            byte[] salt = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(salt);
            return (salt, Array.Empty<byte>());   // verifier computed when SRP bodies are pinned
        });
        return Task.FromResult<(byte[], byte[])?>(creds);
    }

    public Task StoreGameTokenAsync(string loginName, Guid token)
    {
        _tokens[loginName] = token;
        return Task.CompletedTask;
    }
}
