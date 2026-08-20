using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NexusUnleashed.Cryptography;

namespace NexusUnleashed.Sts;

public interface IAccountStore
{
    Task<(byte[] Salt, byte[] Verifier)?> GetSrpCredentialsAsync(string loginName);
    Task StoreGameTokenAsync(string loginName, Guid token);
    Task<long> GetUserIdAsync(string loginName);
}

public static class AuthFlow
{
    private const string KeyLogin = "auth.login";
    private const string KeySrp = "auth.srp";
    private const string KeyAuthed = "auth.ok";
    private const string KeySession = "auth.sessionkey";

    public static void Register(StsServer server, IAccountStore accounts)
    {
        server.On("/Sts/Connect", (s, r) => s.SendAsync(StsReply.Ok(r.Sequence, "")));
        server.On("/Sts/Ping", (s, r) => s.SendAsync(StsReply.Ok(r.Sequence, "")));

        server.On("/Auth/LoginStart", async (s, r) =>
        {
            string login = XmlBody.Field(r.BodyText, "LoginName");
            s.State[KeyLogin] = login;

            var creds = await accounts.GetSrpCredentialsAsync(login);
            if (creds == null || creds.Value.Verifier.Length == 0)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 403));                return;
            }

            var srp = new StsSrp(creds.Value.Salt, creds.Value.Verifier, login);
            byte[] B = srp.StartHandshake();            s.State[KeySrp] = srp;
            Console.WriteLine("[STS-SRP] LoginStart: game-SRP (little-endian)");

            byte[] blob = KeyDataBlob.Pack(creds.Value.Salt, B);
            await s.SendAsync(StsReply.Ok(r.Sequence, KeyDataBody(blob)));
        });

        server.On("/Auth/KeyData", async (s, r) =>
        {
            if (s.State[KeySrp] is not StsSrp srp)
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 409));                return;
            }

            byte[] clientBlob = Convert.FromBase64String(XmlBody.Field(r.BodyText, "KeyData"));
            var parts = KeyDataBlob.Unpack(clientBlob);
            byte[] a = parts.Length > 0 ? parts[0] : Array.Empty<byte>();
            byte[] m1 = parts.Length > 1 ? parts[1] : Array.Empty<byte>();

            if (!srp.Verify(a, m1, out byte[] m2, out byte[] sessionKey))
            {
                Console.WriteLine($"[STS-SRP] KeyData proof did NOT verify (A={a.Length}B, M1={Convert.ToHexString(m1)})");
                await s.SendAsync(StsReply.Error(r.Sequence, 403));                return;
            }
            Console.WriteLine($"[STS-SRP] proof VERIFIED — variant: {srp.MatchedVariant}");
            Console.WriteLine($"[STS-SRP] K={Convert.ToHexString(sessionKey)}");
            s.State[KeyAuthed] = true;
            s.State[KeySession] = sessionKey;

            byte[] blob = KeyDataBlob.Pack(m2);
            await s.SendAsync(StsReply.Ok(r.Sequence, KeyDataBody(blob)));
            s.EnableEncryption(sessionKey);
        });

        server.On("/Auth/LoginFinish", async (s, r) =>
        {
            string login = s.State.TryGetValue(KeyLogin, out var lo) ? (string)lo : "";
            long uid = await accounts.GetUserIdAsync(login);
            string body =
                "<Reply>\n" +
                "<AuthType>Password</AuthType>\n" +
                $"<UserId>{uid}</UserId>\n" +
                "<UserCenter>1</UserCenter>\n" +
                $"<UserName>{System.Security.SecurityElement.Escape(login)}</UserName>\n" +
                "<LocationId>1</LocationId>\n" +
                "<AccessMask>4294967295</AccessMask>\n" +
                "<Status>0</Status>\n" +
                "<Roles>1</Roles>\n" +
                "</Reply>\n";
            await s.SendAsync(StsReply.Ok(r.Sequence, body));
        });

        server.On("/GameAccount/ListMyAccounts", async (s, r) =>
        {
            Console.WriteLine($"[STS] ListMyAccounts req: {r.BodyText.Replace("\n", " ")}");
            string login = s.State.TryGetValue(KeyLogin, out var lo) ? (string)lo : "";
            long uid = await accounts.GetUserIdAsync(login);
            string e = System.Security.SecurityElement.Escape(login) ?? "";
            string alias = login.Contains('@') ? login[..login.IndexOf('@')] : login;
            string ea = System.Security.SecurityElement.Escape(alias) ?? "";
            string body =
                "<Reply>\n<GameAccount>\n" +
                $"<GameAccountId>{uid}</GameAccountId>\n" +
                $"<AccountId>{uid}</AccountId>\n" +
                $"<LoginName>{e}</LoginName>\n" +
                $"<UserId>{uid}</UserId>\n" +
                $"<UserName>{e}</UserName>\n" +
                $"<Email>{e}</Email>\n" +
                $"<Alias>{ea}</Alias>\n" +
                $"<AccountAlias>{ea}</AccountAlias>\n" +
                "<GameCode>wildstar</GameCode>\n" +
                "<AppId>0</AppId>\n" +
                "<UserCenter>1</UserCenter>\n" +
                "<State>1</State>\n" +
                "<Status>0</Status>\n" +
                "<Roles>1</Roles>\n" +
                "</GameAccount>\n</Reply>\n";
            await s.SendAsync(StsReply.Ok(r.Sequence, body));
        });

        server.On("/Auth/RequestGameToken", async (s, r) =>
        {
            if (s.State.TryGetValue(KeyAuthed, out var ok) && ok is true &&
                s.State.TryGetValue(KeyLogin, out var login))
            {
                Guid token = NewToken();
                await accounts.StoreGameTokenAsync((string)login, token);
                long uid = await accounts.GetUserIdAsync((string)login);
                AuthSession.Register(token.ToString("N"), uid);
                await s.SendAsync(StsReply.Ok(r.Sequence,
                    XmlBody.Fields(("Token", token.ToString("N")))));
            }
            else
            {
                await s.SendAsync(StsReply.Error(r.Sequence, 401));            }
        });
    }

    private static string KeyDataBody(byte[] blob)
        => "<Reply>\n<KeyData>" + Convert.ToBase64String(blob) + "</KeyData>\n</Reply>\n";

    private static Guid NewToken()
    {
        Span<byte> b = stackalloc byte[16];
        RandomNumberGenerator.Fill(b);
        return new Guid(b);
    }
}

internal static class XmlBody
{
    public static string Fields(params (string Tag, string Value)[] fields)
    {
        var sb = new System.Text.StringBuilder("<Reply>\n");
        foreach (var (tag, val) in fields)
            sb.Append('<').Append(tag).Append('>')
              .Append(System.Security.SecurityElement.Escape(val))
              .Append("</").Append(tag).Append(">\n");
        return sb.Append("</Reply>\n").ToString();
    }

    public static string Field(string xml, string tag)
    {
        string open = "<" + tag + ">", close = "</" + tag + ">";
        int i = xml.IndexOf(open, StringComparison.Ordinal);
        if (i < 0) return "";
        i += open.Length;
        int j = xml.IndexOf(close, i, StringComparison.Ordinal);
        return j < 0 ? "" : xml[i..j];
    }

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
