using System;
using System.Collections.Generic;
using System.Text;

namespace NexusUnleashed.Sts;

public sealed class StsRequest
{
    public string Method = "POST";
    public string Uri = "";
    public Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
    public byte[] Body = Array.Empty<byte>();

    public int Sequence => Headers.TryGetValue("s", out var s) && int.TryParse(s, out var v) ? v : 0;
    public string BodyText => Encoding.UTF8.GetString(Body);
}

public static class StsReply
{
    public const string Version = "STS/1.0";
    public const string OkStatus = Version + " 200  OK";

    public static byte[] Ok(int sequence, string xmlBody)
        => Build(OkStatus, sequence, Encoding.UTF8.GetBytes(xmlBody));

    public static byte[] OkRaw(int sequence, byte[] body)
        => Build(OkStatus, sequence, body);

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

public sealed class StsParser
{
    private readonly List<byte> _buffer = new();

    public void Feed(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data) _buffer.Add(b);
    }

    public StsRequest? TryReadRequest()
    {
        int headEnd = IndexOfDoubleNewline(out int bodyStart);
        if (headEnd < 0) return null;

        string head = Encoding.ASCII.GetString(_buffer.ToArray(), 0, headEnd);
        string[] lines = head.Split('\n');
        var req = new StsRequest();

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
        if (_buffer.Count < bodyStart + bodyLen) return null;
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
                if (_buffer[i + 1] == (byte)'\n') { bodyStart = i + 2; return i + 1; }
                if (i + 2 < _buffer.Count && _buffer[i + 1] == (byte)'\r' && _buffer[i + 2] == (byte)'\n')
                { bodyStart = i + 3; return i + 1; }
            }
        }
        return -1;
    }
}
