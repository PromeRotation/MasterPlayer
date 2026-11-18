using BossMod;
using BossMod.Components;

namespace MasterPlayer.MechanicRuntime;

/// <summary>Keeps the Divine Favor target running around the narrow ring between successive hits.</summary>
public sealed class PilgrimsTraverse30ChaseHints
{
    private int direction;
    private ulong target;
    private BossModule? arenaModule;
    private readonly List<ShapeDistance> arenaHoles = [];
    public bool Active { get; private set; }
    public WPos? Destination { get; private set; }
    public string Status { get; private set; } = "";

    public void Update(BossModule module, AIHints hints, int slot)
    {
        if (module.PrimaryActor.OID != 0x4609)
        {
            Reset();
            arenaModule = null;
            arenaHoles.Clear();
            return;
        }
        // The MP boundary conversion keeps the outer contour of custom arenas.
        // Preserve the actual hole contour: a 14m circle incorrectly blocks the
        // inner parts of the side platforms needed by Brutal Halo.
        if (!ReferenceEquals(arenaModule, module))
        {
            arenaModule = module;
            arenaHoles.Clear();
            if (module.Bounds is ArenaBoundsCustom custom)
                foreach (var part in custom.Polygon.Parts)
                    for (int i = 0; i < part.HoleStarts.Count; i++)
                        arenaHoles.Add(new SDPolygonWithHoles(new(custom.Center,
                            new RelSimplifiedComplexPolygon(part.Interior(i).ToArray().ToList()))));
        }
        hints.TemporaryObstacles.AddRange(arenaHoles);
        var player = module.WorldState.Party[slot];
        var chase = module.FindComponent<StandardChasingAOEs>();
        var chaser = chase?.Chasers.Find(c => c.Target == player && c.NumRemaining > 0);
        if (player == null || chaser == null)
        {
            Reset();
            return;
        }

        var center = new WPos(-300, -300);
        var angle = (player.Position - center).ToAngle();
        if (target != player.InstanceID || direction == 0)
        {
            target = player.InstanceID;
            direction = Cost(1) <= Cost(-1) ? 1 : -1;
        }
        // Heading changes as the rotation faces the boss. Lock travel direction to
        // the mechanic instead; a fresh goal stays ahead until all eight hits end.
        // A long angular lead spirals toward the hole when repeatedly refreshed.
        // Twenty degrees keeps the pursuit chord within the walkable ring.
        var goal = Point(direction, 20);
        Destination = goal;
        hints.GoalZones.Add(p => p.InCircle(goal, 1.25f) ? 1000f : 0f);
        Active = true;
        Status = $"DivineFavor direction={direction} remaining={chaser.NumRemaining} goal={goal}";

        WPos Point(int sign, float degrees) => center + 16.5f * (angle + (sign * degrees).Degrees()).ToDirection();
        int Cost(int sign)
        {
            var result = 0;
            for (int degrees = 5; degrees <= 60; degrees += 5)
            {
                var point = Point(sign, degrees);
                if (!module.Bounds.Contains(point - module.Center)) result += 100;
                foreach (var obstacle in hints.TemporaryObstacles)
                    if (obstacle.Distance(point) < 0.5f) result += 10;
                foreach (var zone in hints.ForbiddenZones)
                    if (zone.activation <= module.WorldState.FutureTime(3) && zone.shapeDistance.Distance(point) < 0.5f)
                        result++;
            }
            return result;
        }
    }

    public void Reset()
    {
        direction = 0;
        target = 0;
        Active = false;
        Destination = null;
        Status = "";
    }
}
