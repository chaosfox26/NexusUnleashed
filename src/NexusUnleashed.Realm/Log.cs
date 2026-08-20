using System;

namespace NexusUnleashed.Realm;

public static class Log
{
    public static void Info(string m)  => Write("INFO", m);
    public static void Warn(string m)  => Write("WARN", m);
    public static void Error(string m) => Write("ERROR", m);

    private static void Write(string level, string m)
        => Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [{level}] {m}");
}
