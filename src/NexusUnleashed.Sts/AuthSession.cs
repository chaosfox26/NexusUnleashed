// NexusUnleashed - clean-room authored. Shared, in-process bridge between the STS
// login server and the realm channel: STS knows which account authenticated (by
// login/UserId), the realm channel needs it to serve that account's characters.
// Both hosts run in one process, so a small concurrent registry links them.
//
// Keyed by the game token STS issues (so it is account-generic and supports the
// operator's own account by the identical path). LastAccountId is the pragmatic
// correlation for a single active login until the realm-enter (0x0592) token is
// decoded for fully concurrent multi-account routing.
using System.Collections.Concurrent;

namespace NexusUnleashed.Sts;

/// <summary>Links an authenticated STS login to the realm channel that follows.</summary>
public static class AuthSession
{
    private static readonly ConcurrentDictionary<string, long> TokenToAccount = new();

    /// <summary>The most recently authenticated account id (STS UserId).</summary>
    public static long LastAccountId;

    /// <summary>Record that <paramref name="accountId"/> was issued <paramref name="token"/>.</summary>
    public static void Register(string token, long accountId)
    {
        if (!string.IsNullOrEmpty(token))
            TokenToAccount[token] = accountId;
        LastAccountId = accountId;
    }

    /// <summary>Resolve a game token (hex, no dashes) to its account id, or 0.</summary>
    public static long ResolveToken(string token)
        => !string.IsNullOrEmpty(token) && TokenToAccount.TryGetValue(token, out long id) ? id : 0;
}
