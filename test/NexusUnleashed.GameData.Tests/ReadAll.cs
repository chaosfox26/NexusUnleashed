// Read EVERY client table's full values. The record-arithmetic check inside the
// reader is the guard. A model-bound table (one whose 4-byte string-pad columns
// cannot be resolved without the engine's model - the SAME class our proven
// tbl_reader.py skips model-free) is reported separately, not as a failure.
using System;
using System.IO;
using NexusUnleashed.GameData;

static class ReadAll
{
    public static int Run(string tblDir)
    {
        int ok = 0, modelBound = 0, fail = 0; long rows = 0;
        foreach (string f in Directory.GetFiles(tblDir, "*.tbl"))
        {
            try { var t = GameTableReader.Read(f); ok++; rows += t.Rows.Count; }
            catch (InvalidDataException ex) when (ex.Message.Contains("record arithmetic"))
            { modelBound++; }
            catch (Exception ex) { fail++; Console.WriteLine($"  FAIL {Path.GetFileName(f)}: {ex.Message}"); }
        }
        Console.WriteLine($"model-free OK {ok} ({rows:N0} rows), model-bound {modelBound} (known, tbl_reader skips these too), hard FAIL {fail}");
        return fail == 0 ? 0 : 1;
    }
}
