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

    public uint ApplyDamage(uint amount)
    {
        if (Health == 0) return 0;
        uint applied = Math.Min(amount, Health);        uint newHealth = Health - applied;
        if (newHealth == 0 && DelayDeathActive)
        {
            applied = Health - 1;            newHealth = 1;
        }
        Health = newHealth;
        return applied;
    }

    public uint Heal(uint amount)
    {
        if (MaxHealth == 0) return 0;
        uint room = MaxHealth - Health;
        uint applied = Math.Min(amount, room);
        Health += applied;
        return applied;
    }

    public void Revive() => Health = MaxHealth;
}
