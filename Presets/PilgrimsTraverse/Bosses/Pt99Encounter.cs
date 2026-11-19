using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt99Encounter() : PtBossEncounter(99, 0x48EA)
{
    public override uint[] Actions { get; } = [44063, 44069, 44064, 44070, 44072, 44071, 44065, 44066, 44073, 44067, 44081, 44082, 44083, 44084,
        44085, 44086, 45118, 44087, 45115, 45114, 44092, 44090, 44088, 44093, 44091, 44089, 44270, 44314, 44068, 44061, 44062,
        44074, 44075, 44077, 44076, 44078, 44079, 44080];
    private readonly HashSet<uint> pillars = [];
    private readonly Dictionary<ulong, (Vector3 Position, Vector3 Direction)> crystals = [];
    private readonly HashSet<ulong> seenCrystals = [];
    private readonly List<BlazeLine> lines = [];
    private readonly List<Cast> pendingBlazes = [];
    private readonly List<Cast> drains = [];
    public override bool Preparing => !Active && Boss is { Dead: false } && actors.Values.Any(a => a.Oid == 0x48EB && !a.Dead);
    private Vector3 blazeDirection = Vector3.UnitX;
    private int lineIndex;
    private ulong spinelashTarget;
    private DateTime spinelashHit;
    private DateTime chainAt;
    private readonly HashSet<ulong> chainsReleased = [];
    private DateTime fireBaitUntil;
    private static readonly Vector3[] PillarPositions =
    [
        new(-600, 0, -306.99991f), new(-596.50012f, 0, -306.06201f), new(-593.93793f, 0, -303.49991f),
        new(-592.99988f, 0, -299.99991f), new(-593.93781f, 0, -296.49991f), new(-596.50012f, 0, -293.93771f),
        new(-600, 0, -293), new(-603.5f, 0, -293.93781f), new(-606.06219f, 0, -296.5f),
        new(-607.00012f, 0, -300), new(-606.06219f, 0, -303.5f), new(-603.5f, 0, -306.06219f)
    ];
    private static readonly float[] PillarRotations = [0, -30, -60, -89.98f, -120, -150, 0, 150, 120, 89.98f, 60, 30];
    private readonly IGeometryShape[] pillarShapes = PillarPositions.Select((p, i) => PtArenaData.Polygon(p, 2.57745f, 64, Rad(PillarRotations[i]))).ToArray();

    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 44064 or 44070:
                chainAt = cast.Hit.AddSeconds(.1);
                chainsReleased.Clear();
                break;
            case 44073 or 44067: Hit(cast, RayRectangleTemplate.Symmetric(15, 30), "Blade of First Light"); break;
            case 44083: Hit(cast, new CircleTemplate(3), "Bounds of Sin"); break;
            case 45118:
                spinelashTarget = 0;
                Hit(cast, RayRectangleTemplate.Symmetric(4, 60), "Spinelash");
                break;
            case 45115 or 44062:
                if (cast.Action == 44062) fireBaitUntil = default;
                Hit(cast, new CircleTemplate(6), "Terror Eye / Ball of Fire");
                break;
            case 44088 or 44093 or 44091 or 44089:
                drains.RemoveAll(c => c.Source == cast.Source && c.Action == cast.Action);
                drains.Add(cast);
                drains.Sort((a, b) => a.Hit.CompareTo(b.Hit));
                break;
            case 44068 or 44061: fireBaitUntil = cast.Hit.AddSeconds(.1); break;
            case 44079:
                if (!StartBlaze(cast)) pendingBlazes.Add(cast);
                break;
        }
    }

    protected override void SynchronizeActors(DateTime now)
    {
        foreach (var crystal in actors.Values.Where(a => a.Oid == 0x1EBE70 && !a.Dead && seenCrystals.Add(a.Id)))
            crystals[crystal.Id] = (crystal.Position, blazeDirection);
        foreach (var cast in pendingBlazes.ToArray())
            if (StartBlaze(cast) || now > cast.Hit) pendingBlazes.Remove(cast);
    }

    private bool StartBlaze(Cast cast)
    {
        if (crystals.Count == 0) return false;
        var candidate = crystals.MinBy(p => Vector3.DistanceSquared(p.Value.Position, cast.Position));
        if (candidate.Key == 0 || Vector3.DistanceSquared(candidate.Value.Position, cast.Position) > 1) return false;
        var (position, direction) = candidate.Value;
        for (var side = -1; side <= 1; side += 2)
        {
            var step = direction * side;
            var offset = position - Center;
            var range = MathF.Abs(step.X) > .5f ? (18.1f - MathF.Sign(step.X) * offset.X) : (14.1f - MathF.Sign(step.Z) * offset.Z);
            var count = Math.Max(1, (int)range / 4 + 1);
            lines.Add(new(lineIndex++, cast.Source, position, step * 4, cast.Hit, count));
        }
        crystals.Remove(candidate.Key);
        return true;
    }

    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    {
        switch (effect.ActionId)
        {
            case 44074 or 44076: blazeDirection = Vector3.UnitX; break;
            case 44075 or 44077: blazeDirection = Vector3.UnitZ; break;
            case 44082:
                // BMR's +6 was a pathfinding shortcut; its documented actual delay is 11.7s.
                AddHit("bounds/end", "Bounds of Sin 中央爆炸", new CircleTemplate(8), Center, 0, now.AddSeconds(11.7));
                break;
            case 44084: areas.Remove("bounds/end"); break;
            case 44088 or 44093 or 44091 or 44089:
                drains.RemoveAll(c => c.Action == effect.ActionId && c.Source == effect.SourceId);
                break;
            case 44079 or 44080:
                var position = effect.ActionId == 44079 ? effect.SourcePosition ?? effect.EffectPosition : effect.EffectPosition;
                if (position is not { } p) break;
                foreach (var line in lines.Where(l => l.Remaining > 0 && Vector3.DistanceSquared(l.Next, p) < 1
                    && Math.Abs((now - l.Hit).TotalSeconds) < 1).ToArray())
                {
                    if (effect.ActionId == 44080 && effect.SourceRotation is { } rotation &&
                        MathF.Abs(MathF.IEEERemainder(rotation - MathF.Atan2(line.Step.X, line.Step.Z), MathF.Tau)) > Rad(1)) continue;
                    line.Next = p + line.Step;
                    line.Hit = now.AddSeconds(1);
                    --line.Remaining;
                    ++line.Resolved;
                    if (effect.ActionId == 44080) break;
                }
                lines.RemoveAll(l => l.Remaining == 0);
                break;
        }
    }

    protected override void OnMap(uint index, uint state, DateTime now)
    {
        if (index > 11) return;
        if (state == 0x00020001) pillars.Add(index);
        else if (index == 0 && state == 0x00080004) pillars.Clear();
    }
    protected override void OnIcon(ulong target, uint icon, DateTime now)
    { if (icon == 234) { spinelashTarget = target; spinelashHit = now.AddSeconds(6.3); } }
    protected override void OnStatus(ulong actor, ReplayStatus status, bool gained, DateTime now)
    { if (status.Id == 4562 && !gained) chainsReleased.Add(actor); }

    public override bool HoldMovement(DateTime now, ulong player)
        => Status(player, 4562, now) != null || chainAt != default && now >= chainAt.AddSeconds(-.1)
            && now <= chainAt.AddSeconds(2) && !chainsReleased.Contains(player);

    private bool? WantedLight(DateTime now, ulong player)
    {
        if (Preparing)
        {
            var party = actors.Values.Where(a => a.Kind == 1 && !a.Dead && Boundary.Contains(a.Position)).ToArray();
            var light = party.Count(a => Status(a.Id, 4560, now) != null);
            var dark = party.Count(a => Status(a.Id, 4559, now) != null);
            if (Status(player, 4560, now) != null) return light > (party.Length + 1) / 2 ? false : null;
            if (Status(player, 4559, now) != null) return dark > (party.Length + 1) / 2 ? true : null;
            return light == dark ? null : light < dark;
        }
        var drain = drains.FirstOrDefault(c => c.Hit.AddSeconds(.2) >= now);
        if (drain != null) return drain.Action is 44088 or 44089;
        var grief = Boss;
        var eater = actors.Values.FirstOrDefault(a => a.Oid == 0x48EB);
        if (grief == null || eater == null) return null;
        if (eater.Hp <= 1 && Status(player, 4560, now) == null) return true;
        if (grief.Hp <= 1 && Status(player, 4559, now) == null) return false;
        var difference = ((double)eater.Hp - grief.Hp) * 100 / Math.Max(1, grief.MaxHp);
        if (Math.Abs(difference) > 25 || Status(player, 4560, now) == null && Status(player, 4559, now) == null)
            return difference <= 0;
        return null;
    }

    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        foreach (var index in pillars)
            result.Add(new($"pillar/{index}", "场内石柱", new PtShapeTemplate(pillarShapes[index]), Center, 0, now, DateTime.MaxValue, Obstacle: true));
        for (var color = 0; color < 2; ++color)
        {
            var puddles = color == 0 ? PtArenaData.LightPuddles : PtArenaData.DarkPuddles;
            for (var i = 0; i < puddles.Length; ++i)
                result.Add(new($"puddle/{color}/{i}", color == 0 ? "光水" : "暗水", new PtShapeTemplate(puddles[i]), Center, 0, now, DateTime.MaxValue, Safe: true, PreviewOnly: true));
        }
        if (WantedLight(now, player) is { } light)
        {
            var wanted = light ? PtArenaData.LightPuddles : PtArenaData.DarkPuddles;
            var opposite = light ? PtArenaData.DarkPuddles : PtArenaData.LightPuddles;
            var drain = drains.FirstOrDefault(c => c.Hit.AddSeconds(.2) >= now);
            var by = drain?.Hit.AddSeconds(-1) ?? now;
            var buff = Status(player, light ? 4560u : 4559u, now);
            if (buff == null || buff.ExpiresAt < by)
            {
                var shape = new PtCompositeGeometry(wanted);
                result.Add(new("color/goal", light ? "获取光之复仇" : "获取暗之复仇", new PtShapeTemplate(shape), Center, 0, now,
                    drain?.Hit ?? DateTime.MaxValue, Safe: true, Priority: 10, NavigationOnly: true));
                if (drain != null)
                    result.Add(new("color/deadline", "汲取前一秒取得正确颜色", new PtShapeTemplate(new PtCompositeGeometry([Boundary], wanted)),
                        Center, 0, by, drain.Hit, NavigationOnly: true));
            }
            if (drain != null)
                for (var i = 0; i < opposite.Length; ++i)
                    result.Add(new($"color/avoid/{i}", "避开错误颜色", new PtShapeTemplate(opposite[i]), Center, 0, now, drain.Hit, Obstacle: true, NavigationOnly: true));
        }
        if (spinelashTarget == player && now <= spinelashHit.AddSeconds(1))
        {
            var strips = new PtCompositeGeometry([new RectangleGeometry(new(-593, 0, -300), 0, 2, 32), new RectangleGeometry(new(-607, 0, -300), 0, 2, 32)]);
            result.Add(new("spinelash/bait", "Spinelash 避免破窗", new PtShapeTemplate(strips), Center, 0, now, spinelashHit, Safe: true, Priority: 15));
        }
        if (now <= fireBaitUntil)
            foreach (var actor in actors.Values.Where(a => a.Kind == 1 && !a.Dead))
                result.Add(new($"fire/bait/{actor.Id}", "Ball of Fire 诱导", new CircleTemplate(6), actor.Position, 0, now, fireBaitUntil, PreviewOnly: true));
        foreach (var line in lines)
            for (var i = 0; i < line.Remaining; ++i)
            {
                var hit = line.Hit.AddSeconds(i);
                if (hit.AddSeconds(.15) < now) continue;
                result.Add(new($"blaze/{line.Id}/{line.Resolved + i}", "Abyssal Blaze 连续地火", new CircleTemplate(5), line.Next + line.Step * i, 0,
                    hit.AddSeconds(-.2), hit.AddSeconds(.15), line.Owner, NavigationOnly: i >= 2));
            }
    }

    public override IEnumerable<string> Hints(DateTime now, ulong player)
    {
        if (Preparing && Status(player, 4560, now) == null && Status(player, 4559, now) == null && WantedLight(now, player) == null)
            yield return "开战前选择光水或暗水；队伍两色人数保持均衡";
        if (HoldMovement(now, player) || chainAt > now && (chainAt - now).TotalSeconds < 2) yield return "Chains of Condemnation：停止移动，可以继续攻击";
        if (spinelashTarget == player) yield return "Spinelash：在东西侧窄带引导，避免破窗";
        if (WantedLight(now, player) is { } light) yield return light ? "获取光色状态；攻击 Eminent Grief" : "获取暗色状态；攻击 Devoured Eater";
        else if (Status(player, 4559, now) != null) yield return "暗色：攻击 Devoured Eater，优先处理小怪";
        else if (Status(player, 4560, now) != null) yield return "光色：攻击 Eminent Grief，优先处理小怪";
    }
    public override void Cancel(ulong source) { base.Cancel(source); drains.RemoveAll(c => c.Source == source); pendingBlazes.RemoveAll(c => c.Source == source); lines.RemoveAll(l => l.Owner == source); }
    protected override void ResetMechanics()
    {
        pillars.Clear(); crystals.Clear(); seenCrystals.Clear(); lines.Clear(); pendingBlazes.Clear(); drains.Clear(); chainsReleased.Clear();
        lineIndex = 0; spinelashTarget = 0; spinelashHit = chainAt = fireBaitUntil = default; blazeDirection = Vector3.UnitX;
    }
    private sealed class BlazeLine(int id, ulong owner, Vector3 next, Vector3 step, DateTime hit, int remaining)
    {
        public int Id { get; } = id;
        public ulong Owner { get; } = owner;
        public Vector3 Next = next;
        public Vector3 Step { get; } = step;
        public DateTime Hit = hit;
        public int Remaining = remaining;
        public int Resolved;
    }
}
