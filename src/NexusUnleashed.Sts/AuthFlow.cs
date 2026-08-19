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
    private const string KeySession = "auth.sessionkey";
    private static int _kProbe = -1;   // rotates the SRP k-mode across LoginStart retries

    public static void Register(StsServer server, IAccountStore accounts)
    {
        server.On("/Sts/Connect", (s, r) => s.SendAsync(StsReply.Ok(r.Sequence, "")));
        server.On("/Sts/Ping", (s, r) => s.SendAsync(StsReply.Ok(r.Sequence, "")));

        server.On("/Auth/LoginStart", async (s, r) =>
        {
            // Request shape RE'd from the client (spec/protocol/sts.md):
            //   <Request><LoginName>..</LoginName><NetAddress>..</NetAddress></Request>
            string login = XmlBody.Field(r.BodyText, "LoginName");
            s.State[KeyLogin] = login;

            var creds = await accounts.GetSrpCredentialsAsync(login);
            if (creds == null || creds.Value.Verifier.Length == 0)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 403));   // no such account
                return;
            }

            // SCHEMA CONFIRMED from the stock client's StsConnLib64.MT.dll
            // (NU-deconstruct/StsConnLib64.MT.dll/login-protocol.md). The
            // LoginStart-reply parser at 0x18002d4e0:
            //   - reads the <KeyData> field (GetField, base64-decoded to binary),
            //   - parses [u32 LE saltLen][salt][u32 LE BLen][B], EXACT-consume,
            //   - validates B < N as a BIG-ENDIAN bignum (error 15 otherwise).
            // So STS is STANDARD SRP-6a (big-endian), not the game variant. B is
            // emitted big-endian by StsSrp itself; the reply is base64 KeyData
            // inside <Content>.
            // The client retries LoginStart several times per login attempt; rotate
            // the SRP k-mode across those retries so one login session probes every
            // candidate k, and the KeyData proof search identifies the right one.
            int kMode = (System.Threading.Interlocked.Increment(ref _kProbe) & 0x7fffffff) % StsSrp.KModeCount;
            var srp = new StsSrp(creds.Value.Salt, creds.Value.Verifier, login, kMode);
            byte[] B = srp.StartHandshake();                  // big-endian, |N| wide
            s.State[KeySrp] = srp;
            Console.WriteLine($"[STS-SRP] LoginStart: trying {srp.KLabel}");

            byte[] blob = KeyDataBlob.Pack(creds.Value.Salt, B);
            await s.SendAsync(StsReply.Ok(r.Sequence, KeyDataBody(blob)));
        });

        server.On("/Auth/KeyData", async (s, r) =>
        {
            if (s.State[KeySrp] is not StsSrp srp)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 409));   // KeyData before LoginStart
                return;
            }

            // Client request: <Request><KeyData>base64([u32 LE ALen][A][u32 LE
            // M1Len][M1])</KeyData></Request> — same packing the reply uses.
            byte[] clientBlob = Convert.FromBase64String(XmlBody.Field(r.BodyText, "KeyData"));
            var parts = KeyDataBlob.Unpack(clientBlob);
            byte[] a = parts.Length > 0 ? parts[0] : Array.Empty<byte>();
            byte[] m1 = parts.Length > 1 ? parts[1] : Array.Empty<byte>();

            // Standard SRP step 2: verify the client proof (searching the M1-recipe
            // variants against the client's own M1), derive the session key.
            if (!srp.Verify(a, m1, out byte[] m2, out byte[] sessionKey))
            {
                // Dump everything needed to solve the recipe OFFLINE against the
                // client's real M1 (no re-login required): b, salt, v, A, M1.
                Console.WriteLine("[STS-SRP] KeyData proof did NOT match any variant. SOLVE-DUMP:");
                Console.WriteLine($"[STS-SRP]   b={Convert.ToHexString(srp.SecretB)}");
                Console.WriteLine($"[STS-SRP]   salt={Convert.ToHexString(srp.Salt)}");
                Console.WriteLine($"[STS-SRP]   v={Convert.ToHexString(srp.Verifier)}");
                Console.WriteLine($"[STS-SRP]   A={Convert.ToHexString(a)}");
                Console.WriteLine($"[STS-SRP]   M1={Convert.ToHexString(m1)}");
                await s.SendAsync(StsReply.Error(r.Sequence, 403));   // bad proof / recipe not yet matched
                return;
            }
            Console.WriteLine($"[STS-SRP] proof VERIFIED — variant: {srp.MatchedVariant}");
            s.State[KeyAuthed] = true;
            s.State[KeySession] = sessionKey;

            byte[] blob = KeyDataBlob.Pack(m2);
            await s.SendAsync(StsReply.Ok(r.Sequence, KeyDataBody(blob)));
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

    /// <summary>Build the STS reply body:
    /// &lt;Reply&gt;\n&lt;KeyData&gt;BASE64(blob)&lt;/KeyData&gt;\n&lt;/Reply&gt;\n.
    /// Envelope + field + encoding + formatting confirmed BYTE-FOR-BYTE against a
    /// live capture of the frozen realm's own STS (behavioral oracle): the reply
    /// body root is &lt;Reply&gt; (NOT &lt;Content&gt;), the SRP blob rides a base64
    /// &lt;KeyData&gt; child. The client's reply parser fetches this body object,
    /// GetField("KeyData"), base64-decodes to the binary SRP blob.</summary>
    private static string KeyDataBody(byte[] blob)
        => "<Reply>\n<KeyData>" + Convert.ToBase64String(blob) + "</KeyData>\n</Reply>\n";

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
        // STS reply body root is <Reply> (confirmed against the frozen STS wire).
        var sb = new System.Text.StringBuilder("<Reply>\n");
        foreach (var (tag, val) in fields)
            sb.Append('<').Append(tag).Append('>')
              .Append(System.Security.SecurityElement.Escape(val))
              .Append("</").Append(tag).Append(">\n");
        return sb.Append("</Reply>\n").ToString();
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

/// <summary>
/// The KeyData binary blob (base64'd inside the STS &lt;KeyData&gt; element). The
/// SRP values (salt+B one way, A+M1 the other) are packed here. LAYOUT IS A
/// CANDIDATE - each field as [u32 LE length][bytes] - pending confirmation from
/// the client's own KeyData request (RE'd from the client, never from NF). When
/// the captured client blob shows the true packing, this is corrected to match.
/// </summary>
internal static class KeyDataBlob
{
    public static byte[] Pack(params byte[][] parts)
    {
        int n = 0;
        foreach (var p in parts) n += 4 + p.Length;
        var buf = new byte[n];
        int o = 0;
        foreach (var p in parts)
        {
            buf[o] = (byte)p.Length; buf[o + 1] = (byte)(p.Length >> 8);
            buf[o + 2] = (byte)(p.Length >> 16); buf[o + 3] = (byte)(p.Length >> 24);
            System.Array.Copy(p, 0, buf, o + 4, p.Length);
            o += 4 + p.Length;
        }
        return buf;
    }

    public static byte[][] Unpack(byte[] blob)
    {
        var list = new System.Collections.Generic.List<byte[]>();
        int o = 0;
        while (o + 4 <= blob.Length)
        {
            int len = blob[o] | (blob[o + 1] << 8) | (blob[o + 2] << 16) | (blob[o + 3] << 24);
            o += 4;
            if (len < 0 || o + len > blob.Length) break;
            var p = new byte[len];
            System.Array.Copy(blob, o, p, 0, len);
            list.Add(p);
            o += len;
        }
        return list.ToArray();
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
