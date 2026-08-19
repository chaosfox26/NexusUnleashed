// Spline proof: the frozen realm's spline crash modes cannot reproduce.
using System;
using System.Collections.Generic;
using System.Numerics;
using NexusUnleashed.World;

static class SplineTests
{
    public static int Run()
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        // LAW: < 4 nodes rejected
        Check("3 nodes rejected", CatmullRomSpline.TryCreate(new[] { Vector3.Zero, Vector3.One, new Vector3(2,2,2) }) == null);
        Check("null rejected", CatmullRomSpline.TryCreate(null!) == null);
        Check("non-finite node rejected", CatmullRomSpline.TryCreate(new[] {
            Vector3.Zero, new Vector3(float.NaN,0,0), Vector3.One, new Vector3(2,0,0) }) == null);

        // a normal 5-node loop: every sample finite, follower traverses
        var nodes = new List<Vector3>();
        for (int i = 0; i < 6; i++) nodes.Add(new Vector3(MathF.Cos(i)*100, -919f, MathF.Sin(i)*100));
        var spline = CatmullRomSpline.TryCreate(nodes);
        Check("6 nodes accepted", spline != null);
        bool allFinite = true;
        for (int s = 0; s < spline!.SegmentCount; s++)
            for (float u = 0; u <= 1f; u += 0.1f)
                if (!Vec.IsFinite(spline.Evaluate(s, u))) allFinite = false;
        Check("all samples finite", allFinite);
        Check("length finite and positive", float.IsFinite(spline.Length()) && spline.Length() > 0, $"({spline.Length():F0})");

        // DEGENERATE: all-identical nodes must not crash or NaN (the recursion bug)
        var dup = new List<Vector3>();
        for (int i = 0; i < 6; i++) dup.Add(new Vector3(5, -919, 5));
        var ds = CatmullRomSpline.TryCreate(dup);
        bool degenFinite = ds != null;
        if (ds != null)
            for (int s = 0; s < ds.SegmentCount; s++)
                for (float u = 0; u <= 1f; u += 0.25f)
                    if (!Vec.IsFinite(ds.Evaluate(s, u))) degenFinite = false;
        Check("degenerate (all-identical) nodes stay finite - no NaN recursion", degenFinite);

        // follower traverses 5000 ticks without NaN
        var world = new WorldInstance(990u);
        uint g = world.Add(new Entity { Position = nodes[1] });
        var mgr = new MovementManager(world);
        var follower = new SplineFollower(spline, speed: 8f, mode: SplineMode.Loop);
        bool followNaN = false;
        for (int t = 0; t < 5000; t++)
        {
            mgr.Step(g, follower, 0.1f);
            if (!Vec.IsFinite(world.Entities[g].Position)) followNaN = true;
        }
        Check("spline follower: 5000 ticks, zero NaN", !followNaN);

        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
