using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

if (args.Length < 1)
{
    Console.WriteLine("usage: analyzer <packet-dump.log> [outDir]");
    return 2;
}
string logPath = args[0];
string outDir = args.Length > 1 ? args[1] : ".";
Directory.CreateDirectory(outDir);

var line = new Regex(@"^\S+\s+(?<dir>\S+)\s+op=0x(?<op>[0-9A-Fa-f]+)\s+len=(?<len>\d+)\s+(?<hex>[0-9a-f]*)\s*$");

var stats = new Dictionary<(string Dir, uint Op), OpStat>();
long total = 0, parsed = 0;
foreach (string raw in File.ReadLines(logPath))
{
    total++;
    var m = line.Match(raw);
    if (!m.Success) continue;
    parsed++;
    string dir = m.Groups["dir"].Value;
    uint op = uint.Parse(m.Groups["op"].Value, NumberStyles.HexNumber);
    int len = int.Parse(m.Groups["len"].Value);
    string hex = m.Groups["hex"].Value;

    var key = (dir, op);
    if (!stats.TryGetValue(key, out var st)) stats[key] = st = new OpStat();
    st.Count++;
    st.Lengths.Add(len);
    if (st.Samples.Count < 5 && hex.Length > 0) st.Samples.Add(hex.Length > 64 ? hex[..64] + "…" : hex);
}

var rows = stats.OrderBy(kv => kv.Key.Op).ThenBy(kv => kv.Key.Dir).ToList();

string tsv = Path.Combine(outDir, "opcode-inventory.tsv");
using (var w = new StreamWriter(tsv))
{
    w.WriteLine("opcode\tdir\tcount\tminLen\tmaxLen\tdistinctLens\tsamplePayload");
    foreach (var (key, st) in rows)
        w.WriteLine($"0x{key.Op:X4}\t{key.Dir}\t{st.Count}\t{st.Lengths.Min()}\t{st.Lengths.Max()}\t{st.Lengths.Distinct().Count()}\t{(st.Samples.FirstOrDefault() ?? "")}");
}

Console.WriteLine($"lines {total:N0}, parsed {parsed:N0}");
Console.WriteLine($"distinct (dir,opcode) pairs: {stats.Count}");
Console.WriteLine($"  C->S opcodes: {stats.Keys.Where(k => k.Dir == "C->S").Select(k => k.Op).Distinct().Count()}");
Console.WriteLine($"  S->C opcodes: {stats.Keys.Where(k => k.Dir == "S->C").Select(k => k.Op).Distinct().Count()}");
Console.WriteLine($"wrote {tsv}");
Console.WriteLine("busiest opcodes:");
foreach (var (key, st) in rows.OrderByDescending(r => r.Value.Count).Take(10))
    Console.WriteLine($"  {key.Dir} 0x{key.Op:X4}  x{st.Count}  len {st.Lengths.Min()}..{st.Lengths.Max()}");
return 0;

sealed class OpStat
{
    public long Count;
    public List<int> Lengths = new();
    public List<string> Samples = new();
}
