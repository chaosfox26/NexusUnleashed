// NexusUnleashed - clean-room authored. The login flow scaffold over the
// measured STS message set (spec/protocol/sts.md):
//
//   /Sts/Connect -> /Auth/LoginStart -> /Auth/KeyData -> /Auth/LoginFinish
//   -> /Auth/RequestGameToken
//
// The FLOW is pinned (client RTTI). The XML BODY SCHEMAS are UNPINNED - every
// body read/written here is marked and minimal, awaiting one oracle capture.
// SRP6a comes from NexusUnleashed.Cryptography (MIT Arctium seed).
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NexusUnleashed.Sts;

/// <summary>Account lookup abstraction; DB-backed later, in-memory for tests.</summary>
public interface IAccountStore
{
    /// <summary>Returns (salt, verifier) for the account, or null if unknown.</summary>
    Task<(byte[] Salt, byte[] Verifier)?> GetSrpCredentialsAsync(string loginName);
    Task StoreGameTokenAsync(string loginName, Guid token);
}

/// <summary>Registers the login routes on an StsServer.</summary>
public static class AuthFlow
{
    // session-state keys
    private const string KeyLogin = "auth.login";
    private const string KeyConnected = "sts.connected";

    public static void Register(StsServer server, IAccountStore accounts)
    {
        server.On("/Sts/Connect", (s, r) =>
        {
            s.State[KeyConnected] = true;
            return s.SendAsync(StsReply.Ok(r.Sequence, ""));
        });

        server.On("/Sts/Ping", (s, r) => s.SendAsync(StsReply.Ok(r.Sequence, "")));

        server.On("/Auth/LoginStart", async (s, r) =>
        {
            // UNPINNED: exact element name for the account carrying tag awaits
            // capture; accept any single text value in the body for now.
            string login = XmlBody.FirstText(r.BodyText) ?? "";
            s.State[KeyLogin] = login;

            var creds = await accounts.GetSrpCredentialsAsync(login);
            if (creds == null)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 403));
                return;
            }
            // UNPINNED: reply body carries SRP salt + server public B - element
            // names await capture. Flow position is pinned.
            await s.SendAsync(StsReply.Ok(r.Sequence,
                XmlBody.Placeholder("KeyData", "salt+B: UNPINNED body schema")));
        });

        server.On("/Auth/KeyData", (s, r) =>
            // UNPINNED: client A + proof M1 arrive here; verification wires to
            // Cryptography.SRP6a once the body schema is captured.
            s.SendAsync(StsReply.Ok(r.Sequence,
                XmlBody.Placeholder("KeyData", "M2: UNPINNED body schema"))));

        server.On("/Auth/LoginFinish", (s, r) =>
            s.SendAsync(StsReply.Ok(r.Sequence,
                XmlBody.Placeholder("Reply", "UNPINNED body schema"))));

        server.On("/Auth/RequestGameToken", async (s, r) =>
        {
            var token = NewToken();
            if (s.State.TryGetValue(KeyLogin, out var login))
                await accounts.StoreGameTokenAsync((string)login, token);
            await s.SendAsync(StsReply.Ok(r.Sequence,
                XmlBody.Placeholder("Token", token.ToString())));
        });
    }

    private static Guid NewToken()
    {
        Span<byte> b = stackalloc byte[16];
        RandomNumberGenerator.Fill(b);
        return new Guid(b);
    }
}

/// <summary>Tiny helpers for the minimal bodies used while schemas are UNPINNED.</summary>
internal static class XmlBody
{
    public static string Placeholder(string tag, string text)
        => $"<{tag}>{System.Security.SecurityElement.Escape(text)}</{tag}>";

    /// <summary>First text content between any tag pair, or null.</summary>
    public static string? FirstText(string xml)
    {
        int gt = xml.IndexOf('>');
        if (gt < 0) return xml.Trim().Length > 0 ? xml.Trim() : null;
        int lt = xml.IndexOf('<', gt + 1);
        if (lt < 0) return null;
        string inner = xml[(gt + 1)..lt].Trim();
        return inner.Length > 0 ? inner : null;
    }
}
