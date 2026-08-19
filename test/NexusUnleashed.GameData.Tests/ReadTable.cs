using System;
using System.Linq;
using NexusUnleashed.GameData;

static class ReadTable
{
    static int Main(string[] args)
    {
        string tbl = args.Length > 0 ? args[0]
            : @"realm-portable\assets\tbl\Creature2.tbl";
        var t = GameTableReader.Read(tbl);
        Console.WriteLine($"table {t.Name}: {t.Fields.Count} fields, {t.Rows.Count} rows");
        Console.WriteLine("first 6 fields: " + string.Join(", ",
            t.Fields.Take(6).Select(f => $"{f.Name}:{f.Type}")));
        var idCol = 0;
        Console.WriteLine($"row0 Id={t.Rows[0][idCol]}  rowN Id={t.Rows[^1][idCol]}");
        // sanity: Creature2 should have tens of thousands of rows
        bool ok = t.Rows.Count > 1000 && t.Fields.Count > 5;
        Console.WriteLine(ok ? "== GAMETABLE READ OK ==" : "== SUSPECT ==");
        return ok ? 0 : 1;
    }
}
