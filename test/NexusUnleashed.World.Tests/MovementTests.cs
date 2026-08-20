using System;
using System.Numerics;
using NexusUnleashed.World;

static class MovementTests
{
    public static int Run()
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        Check("SafeNormalize(zero) == zero (no NaN)", Vec.SafeNormalize(Vector3.Zero) == Vector3.Zero);
        Check("SafeNormalize(tiny) == zero", Vec.SafeNormalize(new Vector3(1e-9f, 0, 0)) == Vector3.Zero);
        Check("IsFinite rejects NaN", !Vec.IsFinite(new Vector3(float.NaN, 0, 0)));
        Check("IsFinite rejects Infinity", !Vec.IsFinite(new Vector3(0, float.PositiveInfinity, 0)));

        var world = new WorldInstance(990u);
        var home = new Vector3(-500, -919f, -2800);
        uint cid = world.Add(new Entity { Position = home });
        var mgr = new MovementManager(world, new NullTerrain());        var gen = new RandomWander(home, leash: 20f, speed: 5f, seed: 7);
        float minY = float.MaxValue, maxY = float.MinValue; bool anyNaN = false; float maxLeash = 0;
        for (int t = 0; t < 1000; t++)
        {
            mgr.Step(cid, gen, 0.1f);
            var p = world.Entities[cid].Position;
            if (!Vec.IsFinite(p)) anyNaN = true;
            minY = MathF.Min(minY, p.Y); maxY = MathF.Max(maxY, p.Y);
            maxLeash = MathF.Max(maxLeash, Vec.HorizontalDistance(p, home));
        }
        Check("no NaN over 1000 ticks", !anyNaN);
        Check("Y never snapped to 0 or skyward (terrain miss keeps Y)", Math.Abs(minY - (-919f)) < 0.01f && Math.Abs(maxY - (-919f)) < 0.01f, $"(Y stayed {minY:F1}..{maxY:F1})");
        Check("stayed within leash", maxLeash <= 20.5f, $"(max {maxLeash:F1})");

        var w2 = new WorldInstance(3335u);
        var mgr2 = new MovementManager(w2, new NullTerrain());
        var gens = new IMovementGenerator[200];
        var guids = new uint[200];
        var r = new Random(99);
        for (int i = 0; i < 200; i++)
        {
            var h = new Vector3(r.Next(-1000, 1000), -1446f, r.Next(-1000, 1000));
            guids[i] = w2.Add(new Entity { Position = h });
            gens[i] = new RandomWander(h, 15f, 6f, seed: i);
        }
        bool scaleNaN = false;
        for (int t = 0; t < 1000; t++)
            for (int i = 0; i < 200; i++)
            {
                mgr2.Step(guids[i], gens[i], 0.1f);
                if (!Vec.IsFinite(w2.Entities[guids[i]].Position)) scaleNaN = true;
            }
        Check("200 wanderers x 1000 ticks: zero NaN", !scaleNaN);

        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
