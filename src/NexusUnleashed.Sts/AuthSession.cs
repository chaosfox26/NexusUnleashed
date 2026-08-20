using System.Collections.Concurrent;

namespace NexusUnleashed.Sts;

public static class AuthSession
{
    private static readonly ConcurrentDictionary<string, long> TokenToAccount = new();

    public static long LastAccountId;

    public static void Register(string token, long accountId)
    {
        if (!string.IsNullOrEmpty(token))
            TokenToAccount[token] = accountId;
        LastAccountId = accountId;
    }

    public static long ResolveToken(string token)
        => !string.IsNullOrEmpty(token) && TokenToAccount.TryGetValue(token, out long id) ? id : 0;
}
