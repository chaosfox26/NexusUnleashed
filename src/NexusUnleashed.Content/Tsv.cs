// NexusUnleashed - clean-room authored. TSV table reader: the native content
// format of this engine. Header row of column names, tab-separated, UTF-8.
// Our restoration data (spawns, kits, patrols, floors) ships in this format.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NexusUnleashed.Content;

public sealed class TsvTable
{
    public string[] Columns = Array.Empty<string>();
    public List<string[]> Rows = new();

    private Dictionary<string, int>? _index;

    public int Col(string name)
    {
        _index ??= Build();
        return _index.TryGetValue(name, out int i) ? i
            : throw new KeyNotFoundException($"TSV column '{name}' not present");
    }

    public bool HasCol(string name)
    {
        _index ??= Build();
        return _index.ContainsKey(name);
    }

    private Dictionary<string, int> Build()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Columns.Length; i++) d[Columns[i]] = i;
        return d;
    }

    public static TsvTable Read(string path)
    {
        using var reader = new StreamReader(path);
        var t = new TsvTable();
        string? header = reader.ReadLine();
        if (header == null) return t;
        t.Columns = header.Split('\t');
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            t.Rows.Add(line.Split('\t'));
        }
        return t;
    }
}

public static class TsvValue
{
    public static uint U32(string s) => s.Length == 0 ? 0u : uint.Parse(s, CultureInfo.InvariantCulture);
    public static int I32(string s) => s.Length == 0 ? 0 : int.Parse(s, CultureInfo.InvariantCulture);
    public static float F32(string s) => s.Length == 0 ? 0f : float.Parse(s, CultureInfo.InvariantCulture);
    public static ulong U64(string s) => s.Length == 0 ? 0ul : ulong.Parse(s, CultureInfo.InvariantCulture);
}
