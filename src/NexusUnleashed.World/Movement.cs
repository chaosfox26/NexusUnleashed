using System;
using System.Numerics;

namespace NexusUnleashed.World;

public interface IMovementGenerator
{
    Vector3 Next(Vector3 current, float dt);
    bool Done { get; }
}

public sealed class MovementManager
{
    private readonly WorldInstance _world;
    private readonly ITerrainProvider _terrain;

    public MovementManager(WorldInstance world, ITerrainProvider? terrain = null)
    {
        _world = world;
        _terrain = terrain ?? new NullTerrain();
    }

    public void Step(uint guid, IMovementGenerator gen, float dt)
    {
        if (!_world.Entities.TryGetValue(guid, out var e)) return;
        Vector3 proposed = gen.Next(e.Position, dt);
        if (!Vec.IsFinite(proposed)) return;
        float? h = _terrain.HeightAt(_world.WorldId, proposed.X, proposed.Z);
        float y = h ?? e.Position.Y;
        if (!float.IsFinite(y)) y = e.Position.Y;

        _world.Move(guid, new Vector3(proposed.X, y, proposed.Z));
    }
}

public sealed class RandomWander : IMovementGenerator
{
    private readonly Vector3 _home;
    private readonly float _leash;
    private readonly float _speed;
    private readonly Random _rng;
    private Vector3 _target;

    public RandomWander(Vector3 home, float leash, float speed, int seed = 0)
    {
        _home = home; _leash = leash; _speed = speed;
        _rng = new Random(seed);
        _target = home;
    }

    public bool Done => false;
    public Vector3 Next(Vector3 current, float dt)
    {
        if (Vec.HorizontalDistance(current, _target) < 1f)
        {
            double ang = _rng.NextDouble() * Math.PI * 2;
            float r = (float)(_rng.NextDouble()) * _leash;
            _target = new Vector3(_home.X + r * (float)Math.Cos(ang), _home.Y,
                                  _home.Z + r * (float)Math.Sin(ang));
        }
        Vector3 dir = Vec.SafeNormalize(new Vector3(_target.X - current.X, 0, _target.Z - current.Z));
        Vector3 step = dir * (_speed * dt);
        Vector3 next = current + step;
        if (Vec.HorizontalDistance(next, _home) > _leash)
            next = new Vector3(current.X, current.Y, current.Z);
        return next;
    }
}
