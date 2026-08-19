// NexusUnleashed - clean-room authored. STS text-protocol message model.
// Provenance: framing and tokens measured from the client's own
// StsConnLib64.MT.dll (spec/protocol/sts.md). HTTP-shaped text protocol:
//   POST /<Service>/<Message> STS/1.0   |   STS/1.0 <code> <text>
//   l:<body bytes>  s:<sequence>        |   same headers
//   blank line, then an XML body.
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusUnleashed.Sts;

/// <summary>A parsed STS request (client -> server).</summary>
public sealed class StsRequest
{
    public string Method = "POST";
    /// <summary>e.g. "/Auth/LoginStart" — service + message, the routing key.</summary>
    public string Uri = "";
    public Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
    public byte[] Body = Array.Empty<byte>();

    public int Sequence => Headers.TryGetValue("s", out var s) && int.TryParse(s, out var v) ? v : 0;
    public string BodyText => Encoding.UTF8.GetString(Body);
}

/// <summary>Builds an STS reply (server -> client).</summary>
public static class StsReply
{
    public const string Version = "STS/1.0";   // measured client token

    public static byte[] Ok(int sequence, string xmlBody)
        => Build($"{Version} 200 OK", sequence, Encoding.UTF8.GetBytes(xmlBody));

    /// <summary>
    /// 200 OK with a RAW byte body — for replies whose XML carries binary field
    /// values verbatim (the STS &lt;KeyData&gt; SRP blob is raw bytes, not base64;
    /// RE'd from the client, which reads the node value as the bytes directly).
    /// </summary>
    public static byte[] OkRaw(int sequence, byte[] body)
        => Build($"{Version} 200 OK", sequence, body);

    public static byte[] Error(int sequence, int code, string xmlBody = "")
        => Build($"{Version} {code} ERROR", sequence, Encoding.UTF8.GetBytes(xmlBody));

    private static byte[] Build(string statusLine, int sequence, byte[] body)
    {
        var sb = new StringBuilder();
        sb.Append(statusLine).Append("\r\n");
        sb.Append("l:").Append(body.Length).Append("\r\n");
        sb.Append("s:").Append(sequence).Append("R\r\n");
        sb.Append("\r\n");
        byte[] head = Encoding.ASCII.GetBytes(sb.ToString());
        byte[] frame = new byte[head.Length + body.Length];
        head.CopyTo(frame, 0);
        body.CopyTo(frame, head.Length);
        return frame;
    }
}

/// <summary>
/// Incremental parser: feed bytes, get complete requests. Tolerant of partial
/// frames (returns null until a full head+body is buffered).
/// </summary>
public sealed class StsParser
{
    private readonly List<byte> _buffer = new();

    public void Feed(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data) _buffer.Add(b);
    }

    /// <summary>Try to pull one complete request off the buffer.</summary>
    public StsRequest? TryReadRequest()
    {
        // find the blank line that ends the head
        int headEnd = IndexOfDoubleNewline(out int bodyStart);
        if (headEnd < 0) return null;

        string head = Encoding.ASCII.GetString(_buffer.ToArray(), 0, headEnd);
        string[] lines = head.Split('\n');
        var req = new StsRequest();

        // request line: POST /Service/Message STS/1.0
        string[] rl = lines[0].Trim('\r').Split(' ');
        if (rl.Length >= 2) { req.Method = rl[0]; req.Uri = rl[1]; }

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim('\r');
            int colon = line.IndexOf(':');
            if (colon > 0)
                req.Headers[line[..colon]] = line[(colon + 1)..].Trim();
        }

        int bodyLen = req.Headers.TryGetValue("l", out var l) && int.TryParse(l, out var v) ? v : 0;
        if (_buffer.Count < bodyStart + bodyLen) return null;   // body not yet complete

        req.Body = _buffer.GetRange(bodyStart, bodyLen).ToArray();
        _buffer.RemoveRange(0, bodyStart + bodyLen);
        return req;
    }

    private int IndexOfDoubleNewline(out int bodyStart)
    {
        bodyStart = -1;
        for (int i = 0; i + 1 < _buffer.Count; i++)
        {
            if (_buffer[i] == (byte)'\n')
            {
                // \n\n
                if (_buffer[i + 1] == (byte)'\n') { bodyStart = i + 2; return i + 1; }
                // \n\r\n
                if (i + 2 < _buffer.Count && _buffer[i + 1] == (byte)'\r' && _buffer[i + 2] == (byte)'\n')
                { bodyStart = i + 3; return i + 1; }
            }
        }
        return -1;
    }
}
