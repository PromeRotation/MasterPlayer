using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonPendingCast> electrocutionBaits = [];
    private int electrocutionHits;
    private DungeonPendingCast? fumesShrink;
    private bool gyascutusShrunk;
    private ulong chargeTarget;
    private DateTime chargeActivation;
    private DateTime chargeEventTime;
    private ulong sableTarget;

    private bool AeryBossForbidden => Active?.BossOid == 0x39CA && sableTarget != PlayerId
        && _actors.TryGetValue(sableTarget, out var actor) && !actor.Dead && HasStatus(actor, 3324);

    private void AeryCast(DungeonPendingCast cast)
    {
        if (cast.Rule.Action == 3890 && electrocutionBaits.Count == 0)
        {
            electrocutionHits = 0;
            var rule = new DungeonCastRule(3891, 0xEA6, "Electrocution", RayRectangleTemplate.Symmetric(5, 64.9f));
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                electrocutionBaits[member.Id] = cast with { Rule = rule, TargetId = member.Id, FinishAt = cast.FinishAt.AddSeconds(.9) };
        }
        if (cast.Rule.Action == 30181 && !gyascutusShrunk)
            fumesShrink = cast with { Rule = cast.Rule with { Shape = new DonutTemplate(19.9f, 27) },
                Location = new(11.978f, cast.Location.Y, 67.979f), FinishAt = cast.FinishAt.AddSeconds(.8) };
        if (cast.Rule.Action == 30204) sableTarget = cast.TargetId;
    }

    private void AeryFinish(DungeonPendingCast cast, bool completed)
    {
        if (completed) return;
        if (cast.Rule.Action == 3890) { electrocutionBaits.Clear(); electrocutionHits = 0; }
        if (cast.Rule.Action == 30181) fumesShrink = null;
    }

    private void AeryEffect(uint action)
    {
        if (Active?.BossOid == 0xEA6 && action == 3891 && electrocutionBaits.Count > 0
            && ++electrocutionHits >= Math.Min(3, electrocutionBaits.Count))
        {
            electrocutionBaits.Clear();
            electrocutionHits = 0;
        }
    }

    private void AeryTether(ulong target, uint tether, bool present, DateTime timestamp, ulong source)
    {
        if (Active?.BossOid != 0xEA6 || tether != 6 || source != 0 && source != PrimaryId) return;
        chargeEventTime = _now;
        if (present)
        {
            chargeTarget = target;
            chargeActivation = timestamp.AddSeconds(10.1);
        }
        else if (target == 0 || chargeTarget == target) chargeTarget = 0;
    }

    private void SynchronizeAery()
    {
        foreach (var id in electrocutionBaits.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray())
            electrocutionBaits.Remove(id);
        if (Active?.BossOid == 0xEA6 && _now >= chargeEventTime.AddSeconds(.5))
        {
            var target = _actors.GetValueOrDefault(PrimaryId)?.Tethers?.GetValueOrDefault(6u) ?? 0;
            if (target != chargeTarget)
            {
                chargeTarget = target;
                chargeActivation = _now.AddSeconds(10.1);
            }
        }
        if (Active?.BossOid != 0x39CA) return;
        var afflicted = _actors.Values.FirstOrDefault(a => !a.Dead && a.PartySlot >= 0 && HasStatus(a, 3324));
        if (afflicted != null) sableTarget = afflicted.Id;
        else if (!_casts.Values.Any(c => c.Rule.Action == 30204)) sableTarget = 0;
    }

    private void AeryMapEffect(uint index, uint state)
    {
        if (territory != AeryCatalog.TERRITORY || index != 0 || state != 0x00020001) return;
        gyascutusShrunk = true;
        fumesShrink = null;
        if (Active?.BossOid == 0x3970)
            Arena = new CircleGeometry(new(11.978f, _actors[PrimaryId].Position.Y, 67.979f), 19.9f);
    }

    private IEnumerable<DungeonAmbientAoe> AeryAoes()
    {
        foreach (var (id, cast) in electrocutionBaits)
        {
            if (!_actors.TryGetValue(id, out var target) || !_actors.TryGetValue(cast.Source, out var source)) continue;
            var angle = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            yield return new($"aery/electric/{id}", cast.Rule.Name, cast.Rule.Shape!, source.Position, angle,
                Navigate: false, OutlineOnly: id == PlayerId);
            if (id != PlayerId)
                yield return new($"aery/electric/avoid/{id}", cast.Rule.Name, cast.Rule.Shape!, source.Position, angle,
                    Display: false, Activation: cast.FinishAt);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != id && !a.Dead))
                {
                    var dx = member.Position.X - source.Position.X;
                    var dz = member.Position.Z - source.Position.Z;
                    var halfAngle = MathF.Asin(Math.Clamp(2.5f / MathF.Max(.001f, MathF.Sqrt(dx * dx + dz * dz)), 0, 1));
                    yield return new($"aery/electric/separate/{id}/{member.Id}", cast.Rule.Name, new FanTemplate(100, halfAngle * 2),
                        source.Position, MathF.Atan2(dx, dz), Display: false, Activation: cast.FinishAt);
                }
        }
        if (Active?.BossOid == 0xEA6 && chargeTarget == PlayerId && _actors.TryGetValue(chargeTarget, out var marked) && !marked.Dead)
        {
            foreach (var statue in _actors.Values.Where(a => a.Oid == 0xEA8 && !a.Dead))
                yield return new($"aery/statue/{statue.Id}", "将连线传给石像", new CircleTemplate(4), statue.Position, Safe: true);
            var source = _actors[PrimaryId].Position;
            var delta = marked.Position - source;
            var length = new Vector2(delta.X, delta.Z).Length();
            if (length > .001f)
                yield return new("aery/tether", "雷电连线", new RectangleTemplate(.12f, length), (source + marked.Position) * .5f,
                    MathF.Atan2(delta.X, delta.Z), Navigate: false, OutlineOnly: true);
        }
        if (fumesShrink != null)
            yield return new("aery/shrink", "场地缩小", fumesShrink.Rule.Shape!, fumesShrink.Location, Activation: fumesShrink.FinishAt);
        if (Active?.BossOid == 0x3970)
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 31234))
            {
                var center = new Vector3(11.978f, cast.Location.Y, 67.979f);
                yield return new("aery/body-slam/goal", "击退前站在场心9码内", new CircleTemplate(9), center, Safe: true);
                if (!_actors.TryGetValue(PlayerId, out var player) || KnockbackImmune(player, cast.FinishAt)) continue;
                var offset = new Vector2(player.Position.X - cast.Location.X, player.Position.Z - cast.Location.Z);
                if (offset.LengthSquared() <= .0001f) continue;
                var direction = Vector2.Normalize(offset);
                var landing = player.Position + new Vector3(direction.X, 0, direction.Y) * 10;
                var safe = Arena?.Contains(landing) == true;
                yield return new("aery/body-slam/landing", "击退10码落点", new CircleTemplate(.5f), landing,
                    Navigate: false, Safe: safe, OutlineOnly: true);
                yield return new("aery/body-slam/path", "击退轨迹", new RectangleTemplate(.12f, 10), (player.Position + landing) * .5f,
                    MathF.Atan2(direction.X, direction.Y), Navigate: false, Safe: safe);
            }
        if (Active?.BossOid == 0x39CA)
            foreach (var fetter in _actors.Values.Where(a => a.Oid == 0x39CB && !a.Dead))
                yield return new($"aery/fetter/{fetter.Id}", "接近黑锁并救人", new CircleTemplate(fetter.HitboxRadius + 3), fetter.Position,
                    Safe: true, OutlineOnly: true);
    }
}
