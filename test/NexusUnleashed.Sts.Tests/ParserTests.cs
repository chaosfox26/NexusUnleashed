using System;
using System.Text;
using NexusUnleashed.Sts;

int pass = 0, fail = 0;
void Check(string name, bool ok)
{
    if (ok) { pass++; Console.WriteLine($"  PASS {name}"); }
    else { fail++; Console.WriteLine($"  FAIL {name}"); }
}

byte[] Frame(string uri, int seq, string body)
{
    byte[] b = Encoding.UTF8.GetBytes(body);
    return Encoding.ASCII.GetBytes($"POST {uri} STS/1.0\r\nl:{b.Length}\r\ns:{seq}\r\n\r\n")
        is byte[] h ? Combine(h, b) : Array.Empty<byte>();
}
byte[] Combine(byte[] a, byte[] b) { var r = new byte[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }

{
    var p = new StsParser();
    p.Feed(Frame("/Auth/LoginStart", 3, "<Content>chara</Content>"));
    var r = p.TryReadRequest();
    Check("whole frame parses", r != null);
    Check("uri", r!.Uri == "/Auth/LoginStart");
    Check("seq", r.Sequence == 3);
    Check("body", r.BodyText == "<Content>chara</Content>");
    Check("buffer drained", p.TryReadRequest() == null);
}
{
    var p = new StsParser();
    byte[] f = Frame("/Sts/Connect", 1, "<c/>");
    StsRequest? r = null;
    foreach (byte b in f) { p.Feed(new[] { b }); r ??= p.TryReadRequest(); }
    Check("fragmented delivery", r != null && r.Uri == "/Sts/Connect" && r.BodyText == "<c/>");
}
{
    var p = new StsParser();
    p.Feed(Combine(Frame("/Sts/Ping", 5, ""), Frame("/Auth/KeyData", 6, "<k>x</k>")));
    var a = p.TryReadRequest(); var b = p.TryReadRequest();
    Check("pipelined #1", a != null && a.Uri == "/Sts/Ping" && a.Body.Length == 0);
    Check("pipelined #2", b != null && b.Uri == "/Auth/KeyData" && b.BodyText == "<k>x</k>");
}
{
    string reply = Encoding.ASCII.GetString(StsReply.Ok(7, "<T>ok</T>"));
    Check("reply status line", reply.StartsWith("STS/1.0 200 OK\r\n"));
    Check("reply length header", reply.Contains("l:9\r\n"));
    Check("reply body", reply.EndsWith("<T>ok</T>"));
}

Console.WriteLine($"{pass} pass / {fail} fail (parser)");
int live = await LiveSocket.RunAsync();
return (fail == 0 && live == 0) ? 0 : 1;
