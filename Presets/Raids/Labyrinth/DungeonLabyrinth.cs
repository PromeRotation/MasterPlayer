using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsLabyrinth => territory == LabyrinthCatalog.TERRITORY;
    private bool IsLabyrinthBone => IsLabyrinth && Active?.BossOid == 0x92B;
    private bool labyrinthWiping;
    private readonly List<(DungeonPendingCast Cast, DungeonLosTemplate Safe, DungeonLosTemplate Danger)> labyrinthLos = [];

    private void LabyrinthDirector(uint command)
    {
        if (!IsLabyrinth) return;
        if (command == 0x40000005) { labyrinthWiping = true; ResetFight(); }
        if (command == 0x40000011) labyrinthWiping = false;
    }

    private void LabyrinthCast(DungeonPendingCast cast)
    {
        if (!IsLabyrinth || Active?.BossOid != 0x932 || cast.Rule.Action != 1756) return;
        var index = labyrinthLos.FindIndex(v => v.Cast.Source == cast.Source
            && Math.Abs((v.Cast.StartedAt - cast.StartedAt).TotalSeconds) < .25);
        if (index >= 0)
        {
            labyrinthLos[index] = (cast, labyrinthLos[index].Safe, labyrinthLos[index].Danger);
            return;
        }
        var active = _casts.Values.Where(c => c.Rule.Action == 1756).MinBy(c => c.FinishAt)!;
        var comets = _actors.Values.Where(a => a.Oid == 0x936 && !a.Dead).ToArray();
        if (comets.Length == 0) return;
        // CastLineOfSightAOE freezes the alive blockers and active caster origin at each cast start.
        // Comets are passable in the source; standing inside a hitbox is not independently forbidden.
        labyrinthLos.Add((cast, new(new(active.Location, comets, true, 60, false)),
            new(new(active.Location, comets, false, 60, false))));
    }

    private void LabyrinthFinish(DungeonPendingCast cast)
    {
        if (IsLabyrinth && cast.Rule.Action == 1756 && labyrinthLos.Count > 0) labyrinthLos.RemoveAt(0);
    }

    private IEnumerable<DungeonAmbientAoe> LabyrinthAoes()
    {
        if (!IsLabyrinth || Active == null || labyrinthLos.Count == 0) yield break;
        var first = labyrinthLos[0];
        yield return new("labyrinth/meteor/safe", "黄道陨石彗星阴影", first.Safe, first.Cast.Location,
            Safe: true, Navigate: false, Activation: first.Cast.FinishAt);
        yield return new("labyrinth/meteor/danger", "黄道陨石遮挡", first.Danger, first.Cast.Location,
            Display: false, Activation: first.Cast.FinishAt);
    }

    private IEnumerable<string> LabyrinthHints(Vector3? player)
    {
        if (!IsLabyrinth || labyrinthLos.Count == 0 || player is not { } position) yield break;
        if (!labyrinthLos[0].Safe.Geometry.Contains(position)
            && Vector2.DistanceSquared(new(position.X, position.Z),
                new(labyrinthLos[0].Cast.Location.X, labyrinthLos[0].Cast.Location.Z)) < 3600)
            yield return "躲在彗星后";
    }
}
