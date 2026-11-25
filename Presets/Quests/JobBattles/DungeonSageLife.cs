using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Id, DungeonPendingCast Start, int Order, DateTime Activation)> sageRocks = [];
    private readonly Dictionary<ulong, DungeonPendingCast> sageRockStarted = [];
    private readonly List<DungeonPendingCast> sageTowers = [];
    private ulong sageRockId;
    private bool IsAncelMahaud => territory == JobQuestCatalog.LIFE_EPHEMERAL && Active?.BossOid == 0x35C5;
    private bool IsGuildivain => territory == JobQuestCatalog.LIFE_EPHEMERAL && Active?.BossOid == 0x35C6;

    private void SageLifeCast(DungeonPendingCast cast)
    {
        if (!IsAncelMahaud) return;
        if (cast.Rule.Action == 26844)
        {
            var index = sageTowers.FindIndex(c => c.Source == cast.Source);
            if (index >= 0) sageTowers[index] = cast;
            else sageTowers.Add(cast);
        }
        if (cast.Rule.Action != 26832) return;
        if (sageRockStarted.TryGetValue(cast.Source, out var previous)
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75)
        {
            var index = sageRocks.FindIndex(r => ReferenceEquals(r.Start, previous));
            if (index >= 0)
            {
                var rock = sageRocks[index];
                sageRocks[index] = (rock.Id, cast, rock.Order, rock.Order == 0 ? cast.FinishAt : rock.Activation);
            }
        }
        else
            sageRocks.Add((++sageRockId, cast, 0, cast.FinishAt));
        sageRockStarted[cast.Source] = cast;
    }

    private void SageLifeFinish(DungeonPendingCast cast)
    {
        if (!IsAncelMahaud) return;
        if (cast.Rule.Action == 26844) sageTowers.RemoveAll(c => c.Source == cast.Source);
        if (cast.Rule.Action == 26832) sageRockStarted.Remove(cast.Source);
        if (cast.Rule.Action is not (26833 or 26834)) return;
        var order = cast.Rule.Action == 26833 ? 0 : 1;
        var index = sageRocks.FindIndex(r => r.Order == order && MathF.Abs(r.Start.Location.X - cast.Location.X) <= 1
            && MathF.Abs(r.Start.Location.Z - cast.Location.Z) <= 1);
        if (index < 0) return;
        var rock = sageRocks[index];
        if (order == 1) sageRocks.RemoveAt(index);
        else sageRocks[index] = (rock.Id, rock.Start, 1, _now.AddSeconds(2));
    }

    private IEnumerable<DungeonAmbientAoe> SageLifeAoes()
    {
        if (!IsAncelMahaud || Arena == null) yield break;
        foreach (var rock in sageRocks)
            yield return new($"sage/rock/{rock.Id}", "Raw Rockbreaker",
                rock.Order == 0 ? new CircleTemplate(10) : new DonutTemplate(10, 20),
                rock.Start.Location, Activation: rock.Activation);
        if (sageTowers.Count == 0 || !_actors.TryGetValue(PlayerId, out var viewer)) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var enter = new List<IGeometryShape>();
        var avoid = new List<DungeonPendingCast>();
        var inTower = false;
        foreach (var tower in sageTowers)
        {
            var inside = NativeEnemyDistanceSquared(viewer.Position, tower.Location) <= 36;
            var count = party.Count(a => NativeEnemyDistanceSquared(a.Position, tower.Location) <= 36);
            var safe = count < 1 || inside && count == 1;
            inTower |= inside && count == 1;
            yield return new($"sage/tower/{tower.Source}/display", tower.Rule.Name, new CircleTemplate(6), tower.Location,
                Navigate: false, Safe: safe, OutlineOnly: safe || inside);
            if (safe) enter.Add(new CircleGeometry(tower.Location, 6));
            else avoid.Add(tower);
        }
        if (enter.Count > 0)
            yield return new("sage/tower/enter", "进入任一未满单人塔", new SohrKhaiWallHintTemplate(Arena, enter.ToArray()),
                Arena.Position, Display: false, Activation: sageTowers[0].FinishAt);
        else if (!inTower)
            foreach (var tower in avoid)
                yield return new($"sage/tower/{tower.Source}/avoid", "避开已满单人塔", new CircleTemplate(6), tower.Location,
                    Display: false, Activation: sageTowers[0].FinishAt);
    }

    private IEnumerable<DungeonDamagePrediction> SageTowerDamage()
    {
        if (!IsAncelMahaud || sageTowers.Count == 0) yield break;
        foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
            && sageTowers.Any(t => NativeEnemyDistanceSquared(a.Position, t.Location) <= 36)))
            yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, sageTowers[0].FinishAt, "Raidwide");
    }

    private IEnumerable<string> SageLifeHints(Vector3? player)
    {
        if (!IsAncelMahaud || player is not { } position) yield break;
        if (sageRocks.Any(r => (r.Order == 0 ? (GeometryTemplate)new CircleTemplate(10) : new DonutTemplate(10, 20))
            .Resolve(r.Start.Location, 0, null).Contains(position)))
            yield return "GTFO from aoe!";
        var tower = sageTowers.FirstOrDefault(t => NativeEnemyDistanceSquared(position, t.Location) <= 36);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        if (tower != null && party.Count(a => NativeEnemyDistanceSquared(a.Position, tower.Location) <= 36) > 1)
            yield return "Too many soakers in the tower!";
        else if (tower == null && sageTowers.Any(t => !party.Any(a => NativeEnemyDistanceSquared(a.Position, t.Location) <= 36)))
            yield return "Soak the tower!";
    }
}
