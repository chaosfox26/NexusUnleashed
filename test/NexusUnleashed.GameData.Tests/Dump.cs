using System;
using System.Globalization;
using NexusUnleashed.GameData;

static class Dump
{
    public static int Run(string tblPath)
    {
        var t = GameTableReader.Read(tblPath);
        Console.WriteLine(t.Rows.Count + "\t" + t.Fields.Count);
        int[] sample = { 0, 1, 2, t.Rows.Count/2, t.Rows.Count-1 };
        foreach (int r in sample)
        {
            if (r < 0 || r >= t.Rows.Count) continue;
            var row = t.Rows[r];
            var parts = new string[row.Length + 1];
            parts[0] = r.ToString();
            for (int c = 0; c < row.Length; c++)
                parts[c+1] = row[c] is float f ? f.ToString("R", CultureInfo.InvariantCulture) : row[c].ToString() ?? "";
            Console.WriteLine(string.Join("\t", parts).Replace("\n"," ").Replace("\r"," "));
        }
        return 0;
    }
}
