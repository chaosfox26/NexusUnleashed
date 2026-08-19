// NexusUnleashed - clean-room authored. A unit: an entity with health and combat
// state. The health math carries the frozen realm's LAWS:
//   * Damage never drives health below 0 and a single hit is bounded - the
//     irregularities.log guard was "damage exceeding a health pool"; here
//     ApplyDamage clamps and reports the true applied amount.
//   * DelayDeath: while active, lethal damage clamps health to 1 (the unit
//     survives the killing blow until the flag clears).
//   * Healing never exceeds MaxHealth.
using System;

namespace NexusUnleashed.World;

public class UnitEntity : Entity
{
    public uint MaxHealth { get; private set; }
    public uint Health { get; private set; }
    public bool IsAlive => Health > 0;
    public bool DelayDeathActive { get; set; }

    public void InitHealth(uint max)
    {
        MaxHealth = max;
        Health = max;
    }

    /// <summary>Apply damage; returns the amount actually removed. Never overkills the pool.</summary>
    public uint ApplyDamage(uint amount)
    {
        if (Health == 0) return 0;
        uint applied = Math.Min(amount, Health);       // LAW: never exceed the pool
        uint newHealth = Health - applied;
        if (newHealth == 0 && DelayDeathActive)
        {
            applied = Health - 1;                       // LAW: DelayDeath holds at 1
            newHealth = 1;
        }
        Health = newHealth;
        return applied;
    }

    /// <summary>Heal up to MaxHealth; returns the amount actually restored.</summary>
    public uint Heal(uint amount)
    {
        if (MaxHealth == 0) return 0;
        uint room = MaxHealth - Health;
        uint applied = Math.Min(amount, room);
        Health += applied;
        return applied;
    }

    /// <summary>Full reset (respawn).</summary>
    public void Revive() => Health = MaxHealth;
}
