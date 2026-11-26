using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsQueen => Definition.Key == "DRN6Queen";
    private float queenRadius = 29.5f;
    private readonly List<BozjaActor> queenDoom = [];
    private readonly List<BozjaActor> queenDoomPending = [];
    private readonly List<BozjaAoe>?[] queenTiles = new List<BozjaAoe>?[24];
    private readonly int[] queenDistances = new int[24];
    private readonly List<Vector3> queenExcludes = [];
    private readonly List<BozjaAoe> queenCrosses = [];
    private DateTime queenActivation;
    private bool queenShort;

    private void ClearQueen()
    {
        queenRadius = 29.5f;
        queenDoom.Clear();
        queenDoomPending.Clear();
        Array.Clear(queenTiles);
        Array.Clear(queenDistances);
        queenExcludes.Clear();
        queenCrosses.Clear();
        queenActivation = default;
        queenShort = false;
    }

    private void UpdateQueen()
    {
        if (!IsQueen) return;
        for (var i = queenDoomPending.Count - 1; i >= 0; --i)
        {
            var actor = queenDoomPending[i];
            if (!actor.Statuses.Any(s => s?.Id == 910)) queenDoom.Remove(actor);
            queenDoomPending.Remove(actor);
        }
    }

    private void QueenMap(byte index, uint state)
    {
        if (!Active || !IsQueen || index != 0x19) return;
        if (state is not (0x00020001 or 0x00400001 or 0x00200010)) return;
        Arena = state == 0x00200010 ? new RectangleGeometry(Center, 0, 50, 50) : DelubrumArenas.QueenDefault(Center.Y);
        queenRadius = 25;
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.QueenArena);
    }

    private void StartQueen(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.QueenArena && queenRadius > 25)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, 4.8d)));
        }
        if (rule.Kind == BozjaRuleKind.QueenMeansEnds) queenShort = true;
        if (rule.Kind != BozjaRuleKind.QueenKnockback) return;
        foreach (var offset in new[] { 90f, -90f })
            Casts.Add(new(++serial, rule, actor, actor.Actor.Position,
                (spell.Rotation ?? 0) + offset * (MathF.PI / 180f), Activation(actor)));
    }

    private void FinishQueen(BozjaActor actor, ReplayCast spell, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.QueenKnockback && spell.ActionId == 22983)
            Casts.RemoveAll(c => c.Rule == rule);
        if (spell.ActionId is not (22970 or 22971 or 22972 or 22973)) return;
        if (rule.Kind == BozjaRuleKind.QueenChess) queenExcludes.Clear();
        if (rule.Kind == BozjaRuleKind.QueenMeansEnds)
        {
            queenCrosses.Clear();
            queenShort = false;
        }
    }

    private void QueenStatus(BozjaActor actor, ReplayStatus status, bool gain, BozjaRuleKind? only = null)
    {
        if (!Active || !IsQueen) return;
        foreach (var rule in ActiveRules.Where(r => only == null || r.Kind == only))
        {
            if (rule.Kind == BozjaRuleKind.QueenDoom && status.Id == 910)
            {
                if (gain && !queenDoom.Contains(actor)) queenDoom.Add(actor);
                if (!gain) queenDoomPending.Add(actor);
            }
            if (rule.Kind == BozjaRuleKind.QueenMeansEnds && gain && status.Id == 2056 && queenCrosses.Count != 2)
            {
                var distance = QueenIndicatorDistance(status.Param);
                if (distance == 0) continue;
                var position = QueenProjected(actor, distance);
                var cast = new BozjaCast(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(position),
                    actor.Actor.Rotation, Now.AddSeconds(queenShort ? 11.7d : 22d));
                queenCrosses.Add(new(cast, false, queenShort));
            }
            if (rule.Kind != BozjaRuleKind.QueenChess) continue;
            if (status.Id == 2056 && gain && queenExcludes.Count != 2)
            {
                var distance = QueenIndicatorDistance(status.Param);
                if (distance != 0) queenExcludes.Add(QueenProjected(actor, distance));
            }
            else if (status.Id is >= 2474 and <= 2476)
            {
                var slot = ViewerSlot(actor);
                if (slot is < 0 or > 23) continue;
                queenDistances[slot] = gain ? (int)status.Id - 2472 : 0;
                if (gain) queenActivation = Now.AddSeconds(22d);
            }
            else if (status.Id is >= 2480 and <= 2482)
            {
                var slot = ViewerSlot(actor);
                if (slot is < 0 or > 23) continue;
                if (!gain)
                {
                    queenTiles[slot]!.Clear();
                    continue;
                }
                var list = queenTiles[slot] ??= [];
                foreach (var tile in QueenSquares(actor.Actor.Position, (int)status.Id - 2478))
                    list.Add(new(new(++serial, rule, actor, tile, 0, queenActivation), false));
            }
        }
    }

    private static int QueenIndicatorDistance(ushort param) => param switch { 0xE2 => 1, 0xE3 => 2, 0xE4 => 3, _ => 0 };

    private static Vector3 QueenProjected(BozjaActor actor, int distance)
    {
        var (sin, cos) = Math.SinCos(actor.Actor.Rotation);
        return actor.Actor.Position + distance * 10f * new Vector3((float)sin, 0, (float)cos);
    }

    private IEnumerable<Vector3> QueenSquares(Vector3 position, int distance)
    {
        var center = Definition.Center;
        var originI = (int)Math.Round((position.X - center.X) / 10f);
        var originJ = (int)Math.Round((position.Z - center.Z) / 10f);
        var excludes = queenExcludes.Count == 2
            ? queenExcludes.Select(p => ((int)Math.Round((p.X - center.X) / 10f), (int)Math.Round((p.Z - center.Z) / 10f))).ToArray()
            : [(-9, -9), (-9, -9)];
        for (var dx = -5; dx <= 5; ++dx)
            for (var dz = -5; dz <= 5; ++dz)
            {
                if (Math.Abs(dx) + Math.Abs(dz) != distance) continue;
                var i = originI + dx;
                var j = originJ + dz;
                if (Math.Abs(i) <= 2 && Math.Abs(j) <= 2 && excludes.All(e => i != e.Item1 && j != e.Item2))
                    yield return center + new Vector3(i * 10f, 0, j * 10f);
            }
    }

    private IEnumerable<BozjaAoe> QueenAoes(BozjaActor? viewer, BozjaRule rule)
    {
        if (!IsQueen) yield break;
        if (rule.Kind is BozjaRuleKind.QueenArena)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false);
        var slot = viewer == null ? -1 : ViewerSlot(viewer);
        if (slot is < 0 or > 23) yield break;
        if (rule.Kind == BozjaRuleKind.QueenChess)
        {
            if (queenTiles[slot] is { Count: > 0 } tiles)
                foreach (var tile in tiles) yield return tile;
            else if (queenDistances[slot] != 0)
            {
                var index = 0;
                foreach (var point in QueenSquares(viewer!.Actor.Position, queenDistances[slot]))
                    yield return new(new(-1 - slot * 32 - index++, rule, viewer, point, 0, queenActivation), false, false);
            }
        }
        if (rule.Kind != BozjaRuleKind.QueenMeansEnds) yield break;
        if (queenTiles[slot] is { Count: > 0 })
            for (var i = 0; i < queenCrosses.Count; ++i) queenCrosses[i] = queenCrosses[i] with { Risky = true };
        foreach (var cross in queenCrosses) yield return cross;
    }

    private IEnumerable<BozjaHint> QueenHints(BozjaActor viewer, BozjaRule rule)
    {
        if (!IsQueen) yield break;
        if (rule.Kind == BozjaRuleKind.QueenDoom && queenDoom.Count != 0)
        {
            var cleanser = viewer.Actor.ClassJob is 6 or 24 or 28 or 33 or 40 or 23;
            if (queenDoom.Contains(viewer))
                yield return new(cleanser ? "Cleanse yourself! (Doom)." : "You were doomed! Get cleansed fast.");
            else if (cleanser)
                foreach (var affected in queenDoom) yield return new($"Cleanse {affected.Actor.Name}! (Doom)");
        }
        if (rule.Kind == BozjaRuleKind.QueenChess)
        {
            var slot = ViewerSlot(viewer);
            if (slot is < 0 or > 23) yield break;
            if (queenDistances[slot] != 0) yield return new("Prepare for movement!", false);
            else
            {
                var tiles = QueenAoes(viewer, rule).ToArray();
                if (tiles.Length != 0) yield return new("Move to a safe tile!", !tiles.Any(a => a.Cast.Contains(viewer.Actor.Position)));
            }
        }
        if (rule.Kind == BozjaRuleKind.QueenKnockback && QueenMovements(viewer).Any(m => !Arena.Contains(m.To, 0)))
            yield return new("About to be knocked into danger!");
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> QueenMovements(BozjaActor viewer)
    {
        if (!Active || !IsQueen) yield break;
        var from = viewer.Actor.Position;
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.QueenKnockback))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var (sin, cos) = Math.SinCos(cast.Rotation);
            var direction = new Vector3((float)sin, 0, (float)cos);
            var offset = from - cast.Origin;
            var forward = offset.X * direction.X + offset.Z * direction.Z;
            var side = offset.X * direction.Z - offset.Z * direction.X;
            if (!(forward >= 5 && forward <= 60 && MathF.Abs(side) <= 50)) continue;
            var to = from + 20 * direction;
            yield return (cast, from, to);
            from = to;
        }
    }

    private IEnumerable<BozjaZone> QueenNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (!IsQueen) yield break;
        var aoes = QueenAoes(viewer, rule).ToArray();
        if (rule.Kind == BozjaRuleKind.QueenChess && aoes.Length != 0)
            yield return new("queen/chess", rule.Component, aoes[0].Cast.Activation,
                new BozjaQueenChessTemplate(aoes.Where(a => a.Risky).Select(a => a.Cast.Origin).ToArray()), Center, 0);
        if (rule.Kind is BozjaRuleKind.QueenArena or BozjaRuleKind.QueenMeansEnds)
            foreach (var aoe in aoes.Where(a => a.Risky))
                yield return new($"queen/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                    aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (rule.Kind == BozjaRuleKind.QueenKnockback && Casts.FirstOrDefault(c => c.Rule == rule) is { } kb)
            yield return new("queen/knockback", rule.Component, kb.Activation, new DungeonShapeTemplate(QueenKnockbackTriangle(viewer.Actor.Position.Y)),
                default, 0, Goal: true);
    }

    private static IGeometryShape QueenKnockbackTriangle(float y)
    {
        Vector3 Direction(float degrees)
        {
            var (sin, cos) = Math.SinCos(degrees * (MathF.PI / 180f));
            return new((float)sin, 0, (float)cos);
        }
        var origin = new Vector3(-262, y, -415) + 15 * Direction(-90);
        return new WorldPolygonGeometry([origin, origin + 20 * Direction(150), origin + 20 * Direction(30)]);
    }
}
