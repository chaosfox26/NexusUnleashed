// NexusUnleashed - clean-room authored. The login flow over the measured STS
// message set (spec/protocol/sts.md):
//
//   /Sts/Connect -> /Auth/LoginStart -> /Auth/KeyData -> /Auth/RequestGameToken
//
// The FLOW is pinned (client RTTI). The SRP6a crypto underneath is REAL and
// proven (NexusUnleashed.Cryptography). What is still UNPINNED is only the XML
// body layout: until one oracle capture fixes the element names, the SRP values
// (salt, B, A, M1, M2, token) are carried as hex inside minimal <Content> tags.
// When the schema is captured, only the (de)serialization here changes - the
// state machine and crypto stay.
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NexusUnleashed.Cryptography;

namespace NexusUnleashed.Sts;

public interface IAccountStore
{
    /// <summary>Returns (salt, verifier) for the account, or null if unknown.</summary>
    Task<(byte[] Salt, byte[] Verifier)?> GetSrpCredentialsAsync(string loginName);
    Task StoreGameTokenAsync(string loginName, Guid token);
}

public static class AuthFlow
{
    private const string KeyLogin = "auth.login";
    private const string KeySrp = "auth.srp";
    private const string KeyAuthed = "auth.ok";

    public static void Register(StsServer server, IAccountStore accounts)
    {
        server.On("/Sts/Connect", (s, r) => s.SendAsync(StsReply.Ok(r.Sequence, "")));
        server.On("/Sts/Ping", (s, r) => s.SendAsync(StsReply.Ok(r.Sequence, "")));

        server.On("/Auth/LoginStart", async (s, r) =>
        {
            string login = XmlBody.FirstText(r.BodyText) ?? "";
            s.State[KeyLogin] = login;

            var creds = await accounts.GetSrpCredentialsAsync(login);
            if (creds == null || creds.Value.Verifier.Length == 0)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 403));   // no such account / not provisioned
                return;
            }

            // REAL SRP step 1: compute B from salt + verifier, keep the server
            // handshake object on the session for KeyData.
            var srp = new SrpServer(creds.Value.Salt, login, creds.Value.Verifier);
            var (salt, B) = srp.StartHandshake();
            s.State[KeySrp] = srp;

            // UNPINNED body schema: salt + B carried as hex.
            await s.SendAsync(StsReply.Ok(r.Sequence,
                XmlBody.Fields(("salt", Hex.To(salt)), ("B", Hex.To(B)))));
        });

        server.On("/Auth/KeyData", async (s, r) =>
        {
            if (s.State[KeySrp] is not SrpServer srp)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 409));   // KeyData before LoginStart
                return;
            }

            // UNPINNED body schema: client A + proof M1 as hex.
            byte[] a = Hex.From(XmlBody.Field(r.BodyText, "A"));
            byte[] m1 = Hex.From(XmlBody.Field(r.BodyText, "M1"));

            // REAL SRP step 2: verify the client proof, derive the session key.
            SrpServerResult result = srp.Verify(a, m1);
            if (!result.Success)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 403));   // bad password
                return;
            }
            s.State[KeyAuthed] = true;

            await s.SendAsync(StsReply.Ok(r.Sequence,
                XmlBody.Fields(("M2", Hex.To(result.ServerProof)))));
        });

        server.On("/Auth/RequestGameToken", async (s, r) =>
        {
            if (s.State.TryGetValue(KeyAuthed, out var ok) && ok is true &&
                s.State.TryGetValue(KeyLogin, out var login))
            {
                Guid token = NewToken();
                await accounts.StoreGameTokenAsync((string)login, token);
                await s.SendAsync(StsReply.Ok(r.Sequence,
                    XmlBody.Fields(("token", token.ToString("N")))));
            }
            else
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 401));   // not authenticated
            }
        });
    }

    private static Guid NewToken()
    {
        Span<byte> b = stackalloc byte[16];
        RandomNumberGenerator.Fill(b);
        return new Guid(b);
    }
}

/// <summary>Minimal XML body helpers used while schemas are UNPINNED.</summary>
internal static class XmlBody
{
    public static string Fields(params (string Tag, string Value)[] fields)
    {
        var sb = new System.Text.StringBuilder("<Content>");
        foreach (var (tag, val) in fields)
            sb.Append('<').Append(tag).Append('>')
              .Append(System.Security.SecurityElement.Escape(val))
              .Append("</").Append(tag).Append('>');
        return sb.Append("</Content>").ToString();
    }

    /// <summary>Value of a named element, or "".</summary>
    public static string Field(string xml, string tag)
    {
        string open = "<" + tag + ">", close = "</" + tag + ">";
        int i = xml.IndexOf(open, StringComparison.Ordinal);
        if (i < 0) return "";
        i += open.Length;
        int j = xml.IndexOf(close, i, StringComparison.Ordinal);
        return j < 0 ? "" : xml[i..j];
    }

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

internal static class Hex
{
    public static string To(byte[] b)
    {
        var c = new char[b.Length * 2];
        const string h = "0123456789abcdef";
        for (int i = 0; i < b.Length; i++) { c[i * 2] = h[b[i] >> 4]; c[i * 2 + 1] = h[b[i] & 0xF]; }
        return new string(c);
    }

    public static byte[] From(string hex)
    {
        if (string.IsNullOrEmpty(hex) || (hex.Length & 1) == 1) return Array.Empty<byte>();
        var b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return b;
    }
}
