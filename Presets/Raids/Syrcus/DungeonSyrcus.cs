using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsSyrcus => territory == SyrcusCatalog.TERRITORY;
    private bool syrcusWiping;
    private readonly List<(DungeonPendingCast Cast, DungeonLosTemplate Safe, DungeonLosTemplate Danger)> syrcusLos = [];

    private void SyrcusDirector(uint command)
    {
        if (!IsSyrcus) return;
        if (command == 0x40000005) { syrcusWiping = true; ResetFight(); }
        if (command == 0x40000011) syrcusWiping = false;
    }

    private void SyrcusCast(DungeonPendingCast cast)
    {
        if (!IsSyrcus || Active?.BossOid != 0xBE0 || cast.Rule.Action != 2441) return;
        var index = syrcusLos.FindIndex(v => v.Cast.Source == cast.Source
            && Math.Abs((v.Cast.StartedAt - cast.StartedAt).TotalSeconds) < .25);
        if (index >= 0)
        {
            syrcusLos[index] = (cast, syrcusLos[index].Safe, syrcusLos[index].Danger);
            return;
        }
        var active = _casts.Values.Where(c => c.Rule.Action == 2441).MinBy(c => c.FinishAt)!;
        var ice = _actors.Values.Where(a => a.Oid == 0xBE1 && !a.Dead).ToArray();
        if (ice.Length == 0) return;
        // The source freezes the earliest caster origin and living ice at cast start.
        // Ice hitboxes are passable; only the frozen shadow determines shelter.
        syrcusLos.Add((cast, new(new(active.Location, ice, true, 60, false)),
            new(new(active.Location, ice, false, 60, false))));
    }

    private void SyrcusFinish(DungeonPendingCast cast)
    {
        if (IsSyrcus && cast.Rule.Action == 2441 && syrcusLos.Count > 0) syrcusLos.RemoveAt(0);
    }

    private IEnumerable<DungeonAmbientAoe> SyrcusAoes()
    {
        if (!IsSyrcus || Active == null || syrcusLos.Count == 0) yield break;
        var first = syrcusLos[0];
        yield return new("syrcus/curtain/safe", "谢幕冰块阴影", first.Safe, first.Cast.Location,
            Safe: true, Navigate: false, Activation: first.Cast.FinishAt);
        yield return new("syrcus/curtain/danger", "谢幕遮挡", first.Danger, first.Cast.Location,
            Display: false, Activation: first.Cast.FinishAt);
    }

    private IEnumerable<string> SyrcusHints(Vector3? player)
    {
        if (!IsSyrcus || syrcusLos.Count == 0 || player is not { } position) yield break;
        if (!syrcusLos[0].Safe.Geometry.Contains(position)
            && Vector2.DistanceSquared(new(position.X, position.Z),
                new(syrcusLos[0].Cast.Location.X, syrcusLos[0].Cast.Location.Z)) < 3600)
            yield return "躲在冰块后";
    }
}
