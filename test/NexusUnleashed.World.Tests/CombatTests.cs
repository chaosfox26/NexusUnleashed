using System;
using NexusUnleashed.World;

static class CombatTests
{
    public static int Run()
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        var u = new UnitEntity();
        u.InitHealth(1000);
        Check("full health on init", u.Health == 1000 && u.IsAlive);

        Check("normal damage applies exactly", u.ApplyDamage(300) == 300 && u.Health == 700);

        Check("damage never exceeds the pool", u.ApplyDamage(5000) == 700 && u.Health == 0 && !u.IsAlive);

        var d = new UnitEntity(); d.InitHealth(500); d.DelayDeathActive = true;
        uint applied = d.ApplyDamage(9999);
        Check("DelayDeath clamps lethal hit to leave 1", d.Health == 1 && d.IsAlive && applied == 499);
        d.DelayDeathActive = false;
        Check("without DelayDeath the next hit kills", d.ApplyDamage(1) == 1 && !d.IsAlive);

        var h = new UnitEntity(); h.InitHealth(200); h.ApplyDamage(150);        Check("heal restores up to max only", h.Heal(9999) == 150 && h.Health == 200);
        Check("heal at full does nothing", h.Heal(100) == 0 && h.Health == 200);

        var z = new UnitEntity(); z.InitHealth(0);
        Check("zero-max unit takes no damage and no heal", z.ApplyDamage(10) == 0 && z.Heal(10) == 0);

        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
