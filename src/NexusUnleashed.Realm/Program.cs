// NexusUnleashed - clean-room authored. The server host entry point. Boots the
// listening GameServer(s) from config and runs until stopped. This makes the
// engine a runnable server, not just libraries; the login handshake and world
// systems attach to this host as they are built and pinned against the oracle.
using System;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Network;

namespace NexusUnleashed.Realm;

internal static class Program
{
    private static async Task Main()
    {
        Log.Info("NexusUnleashed realm host starting.");
        RealmConfig cfg = RealmConfig.Load("realm.json");
        Log.Info($"bind {cfg.BindAddress} | auth {cfg.AuthPort} | world {cfg.WorldPort}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var world = new GameServer(cfg.BindAddress, cfg.WorldPort);
        // Handlers register here as protocol messages are pinned. Until the
        // handshake spec is captured against the oracle, the host listens and
        // logs — a real, running server skeleton.
        Log.Info("world server listening (handshake pending oracle capture).");

        try
        {
            await world.ListenAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Info("shutdown requested; realm host stopping.");
        }
    }
}
