using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long yuweSerial;
    private int yuweClaws;
    private int yuweBoulders;
    private int yuweEarthShown;
    private bool yuwePrepare;
    private DungeonAmbientAoe? yuweShrink;
    private DungeonAmbientAoe? yuweCell;
    private DungeonAmbientAoe? yuweClaw;
    private readonly HashSet<(ulong Source, DateTime Start)> yuweStarts = [];
    private readonly List<DungeonPendingCast> yuweSimple = [];
    private readonly List<(DungeonPendingCast Cast, bool Risky, Vector4? Color)> yuweVoltage = [];
    private readonly List<(DungeonPendingCast Cast, bool Risky, Vector4? Color)> yuweSoul = [];
    private readonly List<DungeonAmbientAoe> yuweBoulderAoes = [];
    private readonly List<DungeonAmbientAoe> yuweEarth = [];
    private readonly List<DungeonAmbientAoe> yuweRock = [];
    private readonly List<DungeonPendingCast> yuweAngles = [];
    private readonly List<(DungeonPendingCast[] Casts, long Serial)> yuweSmallPatterns = [];
    private DungeonPendingCast? yuweRockCast;
    private Vector3 yuweRockOrigin;
    private readonly List<(DungeonActor Target, DungeonPendingCast Cast)> yuweParty = [];
    private readonly HashSet<ulong> yuweMisdirected = [];
    private readonly HashSet<(ulong Actor, uint Status)> yuweObservedStatuses = [];
    private readonly Dictionary<(ulong Actor, uint Status), bool> yuwePendingStatuses = [];

    public bool RequireYuweyawataMisdirection => territory == YuweyawataCatalog.TERRITORY && Active?.BossOid == 0x464A
        && yuweMisdirected.Contains(PlayerId);

    private void YuweStatus(ulong target, uint status, bool present, bool fromSnapshot = false)
    {
        if (territory != YuweyawataCatalog.TERRITORY || status is not (1422 or 2936 or 3694 or 3909)) return;
        if (!fromSnapshot) yuwePendingStatuses[(target, status)] = present;
        if (present) { yuweObservedStatuses.Add((target, status)); yuweMisdirected.Add(target); }
        else { yuweObservedStatuses.Remove((target, status)); yuweMisdirected.Remove(target); }
    }

    private void YuweObserveStatuses()
    {
        if (territory != YuweyawataCatalog.TERRITORY) return;
        var current = _actors.Values.Where(a => a.PartySlot >= 0)
            .SelectMany(a => new uint[] { 1422, 2936, 3694, 3909 }.Where(s => a.Statuses?.Contains(s) == true)
                .Select(s => (Actor: a.Id, Status: s))).ToHashSet();
        // An event may precede the object-table update. Keep it authoritative until
        // the corresponding raw status acknowledges the change, with no timer.
        foreach (var (key, present) in yuwePendingStatuses.ToArray())
        {
            if (!_actors.ContainsKey(key.Actor) || current.Contains(key) == present) yuwePendingStatuses.Remove(key);
            else if (present) current.Add(key);
            else current.Remove(key);
        }
        foreach (var pair in yuweObservedStatuses.Except(current).ToArray()) YuweStatus(pair.Actor, pair.Status, false, true);
        foreach (var pair in current.Except(yuweObservedStatuses).ToArray()) YuweStatus(pair.Actor, pair.Status, true, true);
        for (var i = 0; i < yuweParty.Count; ++i)
        {
            var item = yuweParty[i];
            item.Target = _actors.GetValueOrDefault(item.Target.Id) ?? item.Target;
            yuweParty[i] = item;
        }
    }

    private void YuweCast(DungeonPendingCast cast)
    {
        if (territory != YuweyawataCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        var fresh = yuweStarts.Add((cast.Source, cast.StartedAt));
        var aoe = new DungeonAmbientAoe($"yuwe/cast/{cast.Source}/{cast.StartedAt.Ticks}", cast.Rule.Name, cast.Rule.Shape ?? new CircleTemplate(5),
            cast.Location, cast.Rotation, Activation: cast.FinishAt);
        if (!fresh)
        {
            var i = yuweSimple.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (i >= 0) yuweSimple[i] = cast;
            i = yuweParty.FindIndex(s => s.Cast.Source == cast.Source && s.Cast.StartedAt == cast.StartedAt);
            if (i >= 0)
            {
                if (_actors.TryGetValue(cast.TargetId, out var corrected)) yuweParty[i] = (corrected, cast);
                else yuweParty.RemoveAt(i);
            }
            foreach (var list in new[] { yuweVoltage, yuweSoul })
            {
                i = list.FindIndex(s => s.Cast.Source == cast.Source && s.Cast.StartedAt == cast.StartedAt);
                if (i >= 0) { var previous = list[i]; previous.Cast = cast; list[i] = previous; }
            }
            i = yuweBoulderAoes.FindIndex(a => a.Id == aoe.Id);
            if (i >= 0) yuweBoulderAoes[i] = aoe;
            if (yuweClaw?.Id == aoe.Id) yuweClaw = aoe;
            if (action is 40661 or 40662) YuweEarthCorrection(cast);
            if (yuweRockCast?.Source == cast.Source && yuweRockCast.StartedAt == cast.StartedAt) YuweRockCorrection(cast);
            return;
        }
        if (cast.Rule.PartyAoe != null && _actors.TryGetValue(cast.TargetId, out var target)) yuweParty.Add((target, cast));
        else if (action is 41121 or 40627 or 41122 or 40625) yuweVoltage.Add((cast, false, null));
        else if (action is 40642 or 40641) yuweSoul.Add((cast, false, null));
        else if (action == 40613) yuweClaw = aoe;
        else if (action is 40607 or 40608) yuweBoulderAoes.Add(aoe);
        else if (cast.Rule.Shape != null)
        {
            yuweSimple.Add(cast);
            if (action is 40656 or 40657 && yuweSimple.Count(c => c.Rule.Action is 40656 or 40657) >= 12)
            {
                var indices = Enumerable.Range(0, yuweSimple.Count).Where(i => yuweSimple[i].Rule.Action is 40656 or 40657).ToArray();
                var ordered = indices.Select(i => yuweSimple[i]).OrderBy(c => c.FinishAt).ToArray();
                for (var i = 0; i < indices.Length; ++i) yuweSimple[indices[i]] = ordered[i];
            }
        }
        if (action is 40640 or 40644)
            yuweShrink = new("yuwe/shrink", "缩场预告", new DonutTemplate(action == 40640 ? 15 : 5, action == 40640 ? 20 : 15),
                QuantizedDungeonPosition(Arena.Position), Activation: cast.FinishAt);
        if (action == 40603)
            yuweShrink = new("yuwe/shrink", "缩场预告", new DonutTemplate(15, 35), Arena.Position, Activation: cast.FinishAt.AddSeconds(.9));
        if (action == 40645) yuwePrepare = true;
        if (action is 40661 or 40662) YuweEarthCast(cast);
        if (action == 40611 && yuweRock.Count == 0 && _actors.TryGetValue(cast.Source, out var caster))
        {
            yuweRockCast = cast;
            yuweRockOrigin = caster.Position;
            var clockwise = YuweClockwise(caster.Position, cast.Rotation);
            for (var i = 0; i < 15; ++i)
                yuweRock.Add(new($"yuwe/rock/{i}", "旋转岩爆", new CircleTemplate(5),
                    YuweRotate(caster.Position, (clockwise ? 1 : -1) * MathF.PI / 8 * i), Activation: cast.FinishAt.AddSeconds(.6 * i)));
        }
    }

    private static bool YuweClockwise(Vector3 position, float rotation)
    {
        Vector2[] positions = [new(34, -697), new(48, -710), new(21, -710), new(34, -724)];
        float[] rotations = [-90.004f, -.003f, 180, 89.999f];
        for (var i = 0; i < 4; ++i)
            if (MathF.Abs(position.X - positions[i].X) <= 1 && MathF.Abs(position.Z - positions[i].Y) <= 1)
                return YuweAngleEqual(rotation, rotations[i] * MathF.PI / 180);
        return false;
    }

    private void YuweRockCorrection(DungeonPendingCast cast)
    {
        var clockwise = YuweClockwise(yuweRockOrigin, cast.Rotation);
        for (var i = 0; i < yuweRock.Count; ++i)
        {
            var a = yuweRock[i];
            var step = int.Parse(a.Id.AsSpan(a.Id.LastIndexOf('/') + 1), System.Globalization.CultureInfo.InvariantCulture);
            yuweRock[i] = a with { Position = YuweRotate(yuweRockOrigin, (clockwise ? 1 : -1) * MathF.PI / 8 * step) };
        }
        yuweRockCast = cast;
    }

    private void YuweFinish(DungeonPendingCast cast)
    {
        if (territory != YuweyawataCatalog.TERRITORY) return;
        var i = yuweSimple.FindIndex(c => c.Source == cast.Source);
        if (i >= 0) yuweSimple.RemoveAt(i);
        i = yuweParty.FindIndex(c => c.Target.Id == cast.TargetId && c.Cast.Rule.Action == cast.Rule.Action);
        if (i >= 0) yuweParty.RemoveAt(i);
        if (cast.Rule.Action is 41121 or 40627 or 41122 or 40625)
        {
            i = yuweVoltage.FindIndex(c => c.Cast.Source == cast.Source);
            if (i >= 0) yuweVoltage.RemoveAt(i);
        }
        if (cast.Rule.Action is 40642 or 40641 && yuweSoul.Count > 0) yuweSoul.RemoveAt(0);
        if (cast.Rule.Action == 40626) yuweCell = null;
        if (cast.Rule.Action == 40606 && yuweEarth.Count > 0) yuweEarth.RemoveAt(0);
        if (cast.Rule.Action == 40611 && yuweRock.Count > 0) yuweRock.RemoveAt(0);
    }

    private void YuweEffect(uint action)
    {
        if (territory != YuweyawataCatalog.TERRITORY) return;
        if (action is 40613 or 40614 && ++yuweClaws == 6) { yuweClaws = 0; yuweClaw = null; }
        if (action is 40607 or 40608 or 40609 && ++yuweBoulders == 30) { yuweBoulders = 0; yuweBoulderAoes.Clear(); }
    }

    private void YuweRemoved(ulong source)
    {
        yuweMisdirected.Remove(source);
        yuweObservedStatuses.RemoveWhere(p => p.Actor == source);
        foreach (var key in yuwePendingStatuses.Keys.Where(p => p.Actor == source).ToArray()) yuwePendingStatuses.Remove(key);
    }

    private void ResetYuwe()
    {
        yuweSerial = 0; yuweClaws = 0; yuweBoulders = 0; yuweEarthShown = 0; yuwePrepare = false;
        yuweShrink = null; yuweCell = null; yuweClaw = null; yuweStarts.Clear(); yuweSimple.Clear();
        yuweVoltage.Clear(); yuweSoul.Clear(); yuweBoulderAoes.Clear(); yuweEarth.Clear(); yuweRock.Clear();
        yuweAngles.Clear(); yuweSmallPatterns.Clear(); yuweParty.Clear(); yuweRockCast = null; yuweRockOrigin = default;
        yuweMisdirected.Clear(); yuweObservedStatuses.Clear(); yuwePendingStatuses.Clear();
    }
}
