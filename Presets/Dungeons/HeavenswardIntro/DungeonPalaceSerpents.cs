using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsPalaceSerpent => territory is HeavenswardIntroCatalog.NINGISHZIDA or HeavenswardIntroCatalog.ALFARD;
    private int palaceSerpentHits;

    private bool IsPalaceSerpentBall(uint action) => IsPalaceSerpent
        && (territory == HeavenswardIntroCatalog.NINGISHZIDA ? action is 6427 or 6428 : action is 7139 or 7140);

    private void PalaceSerpentEffect(uint action)
    {
        if (!IsPalaceSerpent || Active == null) return;
        var fear = territory == HeavenswardIntroCatalog.NINGISHZIDA ? 6429u : 7141u;
        if (IsPalaceSerpentBall(action) || action == fear) palaceSerpentHits = (palaceSerpentHits + 1) % 5;
    }

    private IEnumerable<DungeonAmbientAoe> PalaceSerpentAoes()
    {
        if (!IsPalaceSerpent || Active == null || Arena == null) yield break;
        var middle = new Vector3(-300, Arena.Position.Y, -236);
        GeometryTemplate constraint = palaceSerpentHits < 4 ? new CircleTemplate(11)
            : new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(middle, 5)]);
        yield return new("palace-serpent/bait", "Four puddles then middle", constraint, middle, Display: false, Activation: _now.AddSeconds(10));

        foreach (var boss in _actors.Values.Where(a => a.Oid == Active.BossOid && !a.Dead && a.Targetable && a.Cast == null))
        {
            if (_actors.GetValueOrDefault(boss.TargetId) is not { } target) continue;
            var origin = QuantizedDungeonPosition(boss.Position);
            var angle = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            yield return new($"palace-serpent/cleave/{boss.Id}/display", "Dissever", new FanTemplate(10.8f, MathF.PI / 2),
                origin, angle, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"palace-serpent/cleave/{boss.Id}/nav", "Dissever", new FanTemplate(10.8f, MathF.PI / 2),
                    origin, angle, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"palace-serpent/cleave/{boss.Id}/avoid/{member.Id}", "Do not cleave party", new FanTemplate(100, MathF.PI / 2),
                        origin, MathF.Atan2(member.Position.X - boss.Position.X, member.Position.Z - boss.Position.Z), Display: false);
        }
    }

    private IEnumerable<string> PalaceSerpentHints(Vector3? player)
    {
        if (!IsPalaceSerpent || Active == null) yield break;
        var name = _actors.GetValueOrDefault(PrimaryId)?.Name ?? Active.Name;
        yield return palaceSerpentHits < 4
            ? $"Bait the boss away from the middle of the arena. \n{name} will cast x2 Fire Puddles & x2 Ice Puddles. \nAfter the 4th puddle is dropped, run to the middle."
            : $"Run to the middle of the arena! \n{name} is about to cast a donut AOE!";
        if (player is not { } position) yield break;
        if (TrialVoidzoneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
        foreach (var boss in _actors.Values.Where(a => a.Oid == Active.BossOid && !a.Dead && a.Targetable && a.Cast == null && a.TargetId != PlayerId))
        {
            if (_actors.GetValueOrDefault(boss.TargetId) is not { } target) continue;
            var angle = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            if (new FanTemplate(10.8f, MathF.PI / 2).Resolve(boss.Position, angle, null).Contains(QuantizedDungeonPosition(position)))
            {
                yield return "GTFO from cleave!";
                break;
            }
        }
    }
}
