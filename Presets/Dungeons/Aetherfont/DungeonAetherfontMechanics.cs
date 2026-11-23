using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private float aetherfontGroupRadius;
    private DungeonAmbientAoe? aetherfontShrink;
    private readonly List<DungeonPendingCast> aetherfontCrystals = [];
    private readonly List<DungeonAmbientAoe> aetherfontFissures = [];
    private readonly Dictionary<ulong, DateTime> aetherfontStacks = [];
    private bool IsAetherfontTrash => territory == AetherfontCatalog.TERRITORY && Active != null && AetherfontCatalog.IsTrash(Active.BossOid);
    private bool IsAetherfontOcto => territory == AetherfontCatalog.TERRITORY && Active?.BossOid == 0x3EAA;

    private static float AetherfontRadius(IGeometryShape arena)
        => arena.GetContours().SelectMany(p => p).Max(p => MathF.Max(MathF.Abs(p.X - arena.Position.X), MathF.Abs(p.Z - arena.Position.Z)));

    private static bool AetherfontInBounds(IGeometryShape arena, Vector3 p, float radius)
        => MathF.Abs(p.X - arena.Position.X) <= radius && MathF.Abs(p.Z - arena.Position.Z) <= radius;

    private DungeonDefinition? AetherfontDefinition(DungeonActor actor)
        => Definitions.FirstOrDefault(d => (d.BossOid == actor.Oid || d.PrimaryAliases?.Contains(actor.Oid) == true)
            && (!AetherfontCatalog.IsTrash(d.BossOid) || AetherfontInBounds(d.Arena(actor.Position), actor.Position, AetherfontRadius(d.Arena(actor.Position)))));

    private bool AetherfontCanStart(DungeonActor actor, Vector3 player)
    {
        var definition = AetherfontDefinition(actor);
        if (definition == null) return false;
        return actor.Oid == 0x3EAA ? definition.Arena(actor.Position).Contains(player, 0) : actor.InCombat;
    }

    private void AetherfontSelectPrimary()
    {
        if (!IsAetherfontTrash || Active == null || Arena == null) return;
        if (aetherfontGroupRadius == 0) aetherfontGroupRadius = AetherfontRadius(Arena);
        var alive = _actors.Values.Where(a => !a.Dead && (a.Oid == Active.BossOid || Active.PrimaryAliases?.Contains(a.Oid) == true)
            && AetherfontInBounds(Arena, a.Position, aetherfontGroupRadius)).ToArray();
        if (alive.Length == 0) { ResetFight(); return; }
        if (!alive.Any(a => a.Id == PrimaryId)) PrimaryId = alive[0].Id;
    }

    private void AetherfontCast(DungeonPendingCast cast)
    {
        if (territory != AetherfontCatalog.TERRITORY) return;
        if (cast.Rule.Action is 33339 or 33340)
        {
            var existing = aetherfontCrystals.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (existing >= 0) aetherfontCrystals[existing] = cast;
            else
            {
                aetherfontCrystals.Add(cast);
                if (aetherfontCrystals.Count == 11) aetherfontCrystals.Sort((a, b) => a.FinishAt.CompareTo(b.FinishAt));
            }
        }
        if (cast.Rule.Action == 33364 && Arena != null)
            aetherfontShrink = new("aetherfont/shrink", "缩场预告", new DonutTemplate(10, 15),
                new(425, Arena.Position.Y, -440), Activation: cast.FinishAt.AddSeconds(.8));
    }

    private void AetherfontFinish(DungeonPendingCast cast)
    {
        if (territory != AetherfontCatalog.TERRITORY) return;
        if (cast.Rule.Action is 33339 or 33340)
        {
            var index = aetherfontCrystals.FindIndex(c => c.Source == cast.Source);
            if (index >= 0) aetherfontCrystals.RemoveAt(index);
        }
        if (cast.Rule.Action == 33361 && aetherfontFissures.Count > 0) aetherfontFissures.RemoveAt(0);
    }

    private void AetherfontMap(uint index, uint state, DateTime now)
    {
        if (territory != AetherfontCatalog.TERRITORY || Active?.BossOid != 0x3EEA || Arena == null) return;
        if (index == 0)
        {
            if (state == 0x00020001) { Arena = AetherfontArenas.ArkasSmall(Arena.Position.Y); aetherfontShrink = null; }
            else if (state == 0x00080004) Arena = AetherfontArenas.Arkas(Arena.Position.Y);
        }
        if (index is < 1 or > 4 || state is not (0x00200010 or 0x00020001)) return;
        var pattern = AetherfontFissures.Patterns[index - 1];
        for (var i = 15; i >= 0; --i)
        {
            var delta = pattern.End[i] - pattern.Start[i];
            aetherfontFissures.Add(new($"aetherfont/fissure/{index}/{i}/{now.Ticks}/{aetherfontFissures.Count}", "连续裂隙",
                RayRectangleTemplate.Symmetric(4, delta.Length()), new(pattern.Start[i].X, Arena.Position.Y, pattern.Start[i].Y),
                MathF.Atan2(delta.X, delta.Y), Activation: now.AddSeconds(6)));
        }
    }

    private void AetherfontIcon(ulong actor, uint icon, DateTime now)
    {
        if (territory == AetherfontCatalog.TERRITORY && Active?.BossOid == 0x3EEA && icon == 161)
            aetherfontStacks[actor] = now.AddSeconds(5.2);
    }

    private void AetherfontEffect(uint action, ulong target)
    {
        if (territory != AetherfontCatalog.TERRITORY || Active?.BossOid != 0x3EEA || action != 34712) return;
        if (!aetherfontStacks.Remove(target) && aetherfontStacks.Count > 0) aetherfontStacks.Remove(aetherfontStacks.Keys.First());
    }

    private IEnumerable<DungeonAmbientAoe> AetherfontAoes()
    {
        if (territory != AetherfontCatalog.TERRITORY || Arena == null) yield break;
        if (aetherfontShrink != null) yield return aetherfontShrink;
        if (aetherfontCrystals.Count > 0)
        {
            var deadline = aetherfontCrystals[0].FinishAt.AddSeconds(1);
            foreach (var cast in aetherfontCrystals)
            {
                if (cast.FinishAt >= deadline) break;
                yield return new($"aetherfont/crystal/{cast.Source}", cast.Rule.Name, cast.Rule.Shape!, cast.Location, Activation: cast.FinishAt);
            }
        }
        foreach (var aoe in aetherfontFissures.Take(16)) yield return aoe;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 33342 or 34436
            && _actors.TryGetValue(c.TargetId, out var target) && !target.Dead))
        {
            var radius = cast.Rule.Action == 33342 ? 5 : 6;
            foreach (var aoe in ShisuiBait($"aetherfont/spread/{cast.Source}", cast.TargetId, radius, cast.FinishAt))
                yield return aoe with { Shape = aoe.Display ? aoe.Shape : new CircleTemplate(radius + 1), OutlineOnly = aoe.Display,
                    Position = QuantizedDungeonPosition(aoe.Position) };
        }
        var stacks = aetherfontStacks.Where(s => _actors.TryGetValue(s.Key, out var a) && !a.Dead)
            .Select(s => (Target: s.Key, At: s.Value, Id: $"icon/{s.Key}"))
            .Concat(_casts.Values.Where(c => c.Rule.Action == 33343).Select(c => (Target: c.TargetId, At: c.FinishAt, Id: $"cast/{c.Source}")))
            .Where(s => _actors.TryGetValue(s.Target, out var a) && !a.Dead).ToArray();
        var targets = stacks.Select(s => s.Target).ToHashSet();
        var own = stacks.FirstOrDefault(s => s.Target == PlayerId);
        var spread = _casts.Values.Any(c => c.Rule.Action is 33342 or 34436 && c.TargetId == PlayerId);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        if (own != default)
        {
            var candidates = party.Where(a => !targets.Contains(a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            if (candidates.Length > 0)
                yield return new("aetherfont/stack/gather", "四人分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                    Arena.Position, Display: false, Activation: own.At);
        }
        var join = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var target = _actors[stack.Target];
            var count = party.Count(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(target.Position.X, target.Position.Z)) <= 36);
            var inside = _actors.TryGetValue(PlayerId, out var p) && Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(target.Position.X, target.Position.Z)) <= 36;
            var safe = stack.Target == PlayerId || own == default && !spread && (inside ? count <= 4 : count < 4);
            yield return new($"aetherfont/stack/{stack.Id}/display", "四人分摊", new CircleTemplate(6), QuantizedDungeonPosition(target.Position),
                Navigate: false, OutlineOnly: true, Safe: safe);
            if (stack.Target == PlayerId) continue;
            if (safe) join.Add(new CircleGeometry(target.Position, 6));
            else
                yield return new($"aetherfont/stack/{stack.Id}/avoid", "避免重叠分摊", new CircleTemplate(own == default ? 6 : 12),
                    target.Position, Display: false, Activation: stack.At);
        }
        if (join.Count > 0)
            yield return new("aetherfont/stack/join", "进入四人分摊", new DungeonExclusiveJoinTemplate(Arena, join.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }

    private void ResetAetherfont()
    {
        aetherfontGroupRadius = 0; aetherfontShrink = null;
        aetherfontCrystals.Clear(); aetherfontFissures.Clear(); aetherfontStacks.Clear();
    }
}
