using System;
using System.IO;
using NexusUnleashed.GameData.Gen;

if (args.Length < 4)
{
    Console.WriteLine("usage: gen <tblDir> <outDir> <namespace> <Table>...");
    return 2;
}
string tblDir = args[0], outDir = args[1], ns = args[2];
Directory.CreateDirectory(outDir);
int ok = 0;
for (int i = 3; i < args.Length; i++)
{
    string path = Path.Combine(tblDir, args[i] + ".tbl");
    if (!File.Exists(path)) { Console.WriteLine($"  MISS {args[i]}"); continue; }
    try
    {
        string code = TableCodeGen.Generate(path, ns);
        File.WriteAllText(Path.Combine(outDir, args[i] + ".g.cs"), code);
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL {args[i]}: {ex.GetType().Name}: {ex.Message}");
    }
}
Console.WriteLine($"{ok} generated");
return 0;
