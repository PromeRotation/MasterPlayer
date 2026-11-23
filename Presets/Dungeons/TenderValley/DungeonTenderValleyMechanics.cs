using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long tenderSerial;
    private bool tenderSmall;
    private bool tenderCactiActive;
    private int tenderNeedlesCount;
    private DungeonAmbientAoe? tenderShrink;
    private readonly HashSet<ulong> tenderObserved = [];
    private readonly HashSet<ulong> tenderTetherObserved = [];
    private readonly HashSet<(ulong Source, DateTime Start)> tenderStarts = [];
    private readonly List<DungeonPendingCast> tenderSimple = [];
    private readonly List<DungeonAmbientAoe> tenderCacti = [];
    private readonly List<DungeonAmbientAoe> tenderStatues = [];
    private readonly List<(IGeometryShape[] Tiles, int Index, DateTime At, long Serial)> tenderLabyrinth = [];
    private readonly List<ulong> tenderSludge = [];
    private readonly List<(DungeonActor Target, uint Action, float Radius, DateTime At, long Serial, ulong Source, DateTime Start)> tenderStacks = [];
    private readonly List<(DungeonActor Target, uint Action, DateTime At, long Serial, ulong Source, DateTime Start)> tenderSpreads = [];
    private readonly List<(DungeonActor Target, DateTime At, long Serial)> tenderBaits = [];

    private void TenderObserveActors()
    {
        if (territory != TenderValleyCatalog.TERRITORY || Active == null) return;
        foreach (var a in _actors.Values)
        {
            if (!tenderObserved.Add(a.Id)) continue;
            if (Active.BossOid == 0x4234 && a.Oid is 0x1EBBF0 or 0x1EBBF1)
                tenderCacti.Add(new($"tender/cactus/{++tenderSerial}", "仙人掌爆炸", new CircleTemplate(a.Oid == 0x1EBBF0 ? 6 : 11),
                    QuantizedDungeonPosition(a.Position), Activation: DateTime.MinValue));
            if (Active.BossOid == 0x4164 && a.Oid is 0x1EBA86 or 0x1EBA87 or 0x1EBA88)
                tenderSludge.Add(a.Id);
            if (Active.BossOid == 0x4253 && a.Tethers?.ContainsKey(37) == true && !tenderTetherObserved.Contains(a.Id))
                TenderTether(a.Id, 37, true, _now);
        }
    }

    private void TenderCast(DungeonPendingCast cast)
    {
        if (territory != TenderValleyCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        var fresh = tenderStarts.Add((cast.Source, cast.StartedAt));
        if (cast.Rule.PartyAoe != null && _actors.TryGetValue(cast.TargetId, out var target))
        {
            if (cast.Rule.PartyAoe == "分摊")
            {
                var i = tenderStacks.FindIndex(s => s.Source == cast.Source && s.Start == cast.StartedAt);
                var value = (target, action, 6f, cast.FinishAt, ++tenderSerial, cast.Source, cast.StartedAt);
                if (i >= 0) tenderStacks[i] = value with { Item5 = tenderStacks[i].Serial };
                else if (fresh) tenderStacks.Add(value);
            }
            else
            {
                var i = tenderSpreads.FindIndex(s => s.Source == cast.Source && s.Start == cast.StartedAt);
                var value = (target, action, cast.FinishAt, ++tenderSerial, cast.Source, cast.StartedAt);
                if (i >= 0) tenderSpreads[i] = value with { Item4 = tenderSpreads[i].Serial };
                else if (fresh) tenderSpreads.Add(value);
            }
        }
        else if (cast.Rule.Shape != null)
        {
            var i = tenderSimple.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (i >= 0) tenderSimple[i] = cast;
            else if (fresh) tenderSimple.Add(cast);
            var group = TenderSimpleGroup(action);
            if (fresh && group != 0)
            {
                var indices = Enumerable.Range(0, tenderSimple.Count).Where(n => TenderSimpleGroup(tenderSimple[n].Rule.Action) == group).ToArray();
                if (indices.Length >= 99)
                {
                    var ordered = indices.Select(n => tenderSimple[n]).OrderBy(c => c.FinishAt).ToArray();
                    for (var n = 0; n < indices.Length; ++n) tenderSimple[indices[n]] = ordered[n];
                }
            }
        }
        if (action == 37384 && !tenderSmall)
            tenderShrink = new("tender/shrink", "缩场预告", VanguardRing(Arena.Position, new(25, 25), new(20, 20)),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(.7));
        if (action == 37387 && fresh)
        {
            tenderCactiActive = true;
            for (var i = 0; i < Math.Min(8, tenderCacti.Count); ++i)
                tenderCacti[i] = tenderCacti[i] with { Activation = cast.FinishAt.AddSeconds(13.7) };
        }
    }

    private static uint TenderSimpleGroup(uint action) => action switch
    {
        39154 or 39155 => 39154,
        36401 or 36402 => 36401,
        36545 or 36551 => 36545,
        _ => 0,
    };

    private void TenderFinish(DungeonPendingCast cast)
    {
        if (territory != TenderValleyCatalog.TERRITORY || Arena == null) return;
        // Component caster lists consume one matching caster; predictions survive elapsed activation.
        var simple = tenderSimple.FindIndex(c => c.Source == cast.Source);
        if (simple >= 0) tenderSimple.RemoveAt(simple);
        var stack = tenderStacks.FindIndex(s => s.Action == cast.Rule.Action && s.Target.Id == cast.TargetId);
        if (stack >= 0) tenderStacks.RemoveAt(stack);
        var spread = tenderSpreads.FindIndex(s => s.Action == cast.Rule.Action && s.Target.Id == cast.TargetId);
        if (spread >= 0) tenderSpreads.RemoveAt(spread);
        if (cast.Rule.Action is 37388 or 37389 && tenderCacti.Count > 0)
        {
            tenderCacti.RemoveAt(0);
            tenderCactiActive = false;
        }
        if (cast.Rule.Action == 36748 && !tenderSmall)
            tenderShrink = new("tender/shrink", "缩场预告", VanguardRing(Arena.Position, new(15, 15), new(12, 12)),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(4.8));
    }

    private void TenderIcon(ulong target, uint icon, DateTime now)
    {
        if (territory != TenderValleyCatalog.TERRITORY || Active?.BossOid != 0x4164 || !_actors.TryGetValue(target, out var actor)) return;
        if (icon == 341) tenderBaits.Add((actor, now.AddSeconds(5.3), ++tenderSerial));
        if (icon == 139) tenderSpreads.Add((actor, 36755, now.AddSeconds(5.1), ++tenderSerial, 0, now));
        var (action, radius) = icon switch { 62 => (39983u, 6f), 542 => (39982u, 5f), 543 => (39981u, 4f), _ => (0u, 0f) };
        if (action != 0) tenderStacks.Add((actor, action, radius, now.AddSeconds((double)4.6f), ++tenderSerial, 0, now));
    }

    private void TenderEffect(uint action, ulong target)
    {
        if (territory != TenderValleyCatalog.TERRITORY) return;
        if (action == 37386) ++tenderNeedlesCount;
        if (action is 39823 or 39824 or 39825) tenderStatues.Clear();
        if (action == 36757 && tenderBaits.Count > 0) tenderBaits.RemoveAt(0);
        if (action is 39983 or 39982 or 39981)
        {
            var i = tenderStacks.FindIndex(s => s.Action == action && s.Target.Id == target);
            if (i < 0) i = tenderStacks.FindIndex(s => s.Action == action);
            if (i >= 0) tenderStacks.RemoveAt(i);
        }
        if (action == 36755)
        {
            var i = tenderSpreads.FindIndex(s => s.Action == action && s.Target.Id == target);
            if (i < 0) i = tenderSpreads.FindIndex(s => s.Action == action);
            if (i >= 0) tenderSpreads.RemoveAt(i);
        }
    }

    private void TenderTether(ulong source, uint tether, bool present, DateTime now)
    {
        if (territory != TenderValleyCatalog.TERRITORY || Active?.BossOid != 0x4253 || tether != 37 || !present
            || !_actors.TryGetValue(source, out var actor)) return;
        tenderTetherObserved.Add(source);
        var p = actor.Position;
        var anchors = new (float X, float Z, float Length)[] { (-112.5f, -486.5f, 22), (-147.5f, -471.5f, 22),
            (-147.5f, -486.5f, 12), (-112.5f, -471.5f, 12) };
        var length = anchors.FirstOrDefault(a => MathF.Abs(a.X - p.X) <= 1 && MathF.Abs(a.Z - p.Z) <= 1).Length;
        if (length == 0) length = 35;
        tenderStatues.Add(new($"tender/statue/{++tenderSerial}", "雕像直线攻击", RayRectangleTemplate.Symmetric(8, length),
            QuantizedDungeonPosition(p), actor.Rotation, Activation: now.AddSeconds(8.1)));
    }

    private void TenderMap(uint index, uint state, DateTime now)
    {
        if (territory != TenderValleyCatalog.TERRITORY || Arena == null) return;
        if (state == 0x00020001 && (Active?.BossOid == 0x4234 && index == 3 || Active?.BossOid == 0x4164 && index == 0))
        {
            tenderSmall = true; tenderShrink = null;
            var width = Active.BossOid == 0x4234 ? 40 : 24;
            Arena = new RectangleGeometry(Arena.Position, 0, width, width);
        }
        if (Active?.BossOid != 0x4164 || index != 1) return;
        if (state is 0x00100004 or 0x00200004 or 0x00400004 or 0x00080004) tenderLabyrinth.Clear();
        var pair = state switch { 0x01000080 => 0, 0x04000200 => 1, 0x10000800 => 2, 0x00020001 => 3, _ => -1 };
        if (pair < 0) return;
        var (a, b) = pair switch
        {
            0 => (new Vector2(-124, -552), new Vector2(-140, -564)),
            1 => (new Vector2(-128, -560), new Vector2(-120, -544)),
            2 => (new Vector2(-132, -548), new Vector2(-120, -564)),
            _ => (new Vector2(-136, -556), new Vector2(-140, -544)),
        };
        tenderLabyrinth.Add(([new RectangleGeometry(new(a.X, Arena.Position.Y, a.Y), 0, 4, 4),
            new RectangleGeometry(new(b.X, Arena.Position.Y, b.Y), 0, 4, 4)], pair, now.AddSeconds(10), ++tenderSerial));
    }

    private void SynchronizeTender()
    {
        if (territory != TenderValleyCatalog.TERRITORY) return;
        for (var i = 0; i < tenderStacks.Count; ++i)
        {
            var s = tenderStacks[i]; s.Target = _actors.GetValueOrDefault(s.Target.Id) ?? s.Target; tenderStacks[i] = s;
        }
        for (var i = 0; i < tenderSpreads.Count; ++i)
        {
            var s = tenderSpreads[i]; s.Target = _actors.GetValueOrDefault(s.Target.Id) ?? s.Target; tenderSpreads[i] = s;
        }
        for (var i = 0; i < tenderBaits.Count; ++i)
        {
            var b = tenderBaits[i]; b.Target = _actors.GetValueOrDefault(b.Target.Id) ?? b.Target; tenderBaits[i] = b;
        }
        tenderSpreads.RemoveAll(s => s.Source == 0 && s.Target.Dead);
        tenderBaits.RemoveAll(b => b.Target.Dead);
        tenderSludge.RemoveAll(id => !_actors.ContainsKey(id));
    }

    private void ResetTender()
    {
        tenderSerial = 0; tenderSmall = false; tenderCactiActive = false; tenderNeedlesCount = 0; tenderShrink = null;
        tenderObserved.Clear(); tenderTetherObserved.Clear(); tenderStarts.Clear(); tenderSimple.Clear(); tenderCacti.Clear();
        tenderStatues.Clear(); tenderLabyrinth.Clear(); tenderSludge.Clear(); tenderStacks.Clear(); tenderSpreads.Clear(); tenderBaits.Clear();
    }
}
