using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed class EnuoEncounter
{
    private readonly Dictionary<ulong, ReplayActorState> _actors = [];
    private readonly Dictionary<ulong, EnuoCast> _casts = [];
    private readonly Dictionary<ulong, (uint Action, DateTime Start)> _seen = [];
    private readonly Dictionary<(uint Icon, ulong Target), DateTime> _icons = [];
    private readonly Dictionary<(uint Id, ulong Source), (ulong Target, DateTime At)> _tethers = [];
    private readonly HashSet<ulong> _consumedOrbs = [];
    private readonly Dictionary<uint, ulong[]> _orbAssignments = [];
    private readonly HashSet<(uint Action, uint Sequence)> _effects = [];
    private readonly Dictionary<ulong, Vector3> _orbOrigins = [];
    private readonly EnuoTowerWave _towerWave = new();
    private ulong[] _party = [];
    private ulong _local;
    private DateTime _now, _dimensionUntil, _orbUntil, _chaseUntil, _flareUntil, _freezeUntil;
    private readonly HashSet<ulong> _flares = [];
    private float? _chargeBearing;
    private Vector3? _freezeStep;
    private int _dimensionHits;
    private bool _adds;
    private uint _wave;
    private DateTime _waveUntil, _waveStart;
    public IReadOnlyDictionary<ulong, EnuoCast> Casts => _casts;
    public IReadOnlyDictionary<ulong, ReplayActorState> Actors => _actors;
    public int Role => Array.IndexOf(_party, _local);
    public static uint[] ClaimedActions => [49369, 48475, .. Enumerable.Range(49972, 79).Select(i => (uint)i)];

    public void Reset()
    {
        _actors.Clear(); _party = []; _local = 0;
        _casts.Clear(); _seen.Clear(); _icons.Clear(); _tethers.Clear(); _effects.Clear();
        _consumedOrbs.Clear(); _orbAssignments.Clear(); _orbOrigins.Clear();
        _dimensionUntil = _orbUntil = _chaseUntil = _waveUntil = _flareUntil = _freezeUntil = default; _flares.Clear();
        _dimensionHits = 0; _wave = 0; _adds = false;
        _chargeBearing = null; _freezeStep = null;
        _towerWave.Reset();
    }

    public void Synchronize(IEnumerable<ReplayActorState> actors, IEnumerable<ReplayPartyMember> party, ulong local, DateTime now)
    {
        if (_now != default && now < _now) Reset();
        _now = now; _local = local; _party = EnuoStrategy.ResolveParty(party);
        _actors.Clear();
        foreach (var actor in actors)
        {
            _actors[actor.Id] = actor;
            if (actor.Oid is 0x4DC5 or 0x4DC6 && !_orbOrigins.ContainsKey(actor.Id)) _orbOrigins[actor.Id] = actor.Position;
            if (actor.Cast is { EffectSeen: false } c && c.FinishAt > now)
                StartCast(new(actor.Id, c.ActionId, c.TargetId, c.Location, c.Rotation ?? actor.Rotation, c.StartedAt, c.FinishAt));
        }
        // The actor snapshot is authoritative for status duration; tethers are also recovered on mid-fight enable.
        foreach (var actor in _actors.Values)
            foreach (var tether in actor.Tethers)
                Tether(actor.Id, tether.TargetId, tether.Id, now);
        Tick(now);
    }

    public void Tick(DateTime now)
    {
        if (_now != default && now < _now) Reset();
        _now = now;
        foreach (var source in _casts.Where(p => now > p.Value.Finish.AddSeconds(p.Value.Action is >= 49995 and <= 49997 ? 3 : 0.6)).Select(p => p.Key).ToArray()) _casts.Remove(source);
        foreach (var k in _icons.Where(p => now > p.Value).Select(p => p.Key).ToArray()) _icons.Remove(k);
        foreach (var k in _tethers.Where(p => now > p.Value.At.AddSeconds(p.Key.Id == 284 ? 120 : 30)).Select(p => p.Key).ToArray()) _tethers.Remove(k);
        foreach (var k in _seen.Where(p => now > p.Value.Start.AddSeconds(120)).Select(p => p.Key).ToArray()) _seen.Remove(k);
        if (now > _orbUntil) { _orbAssignments.Clear(); _consumedOrbs.Clear(); }
    }

    public void StartCast(EnuoCast cast, bool authoritative = false)
    {
        if (!ClaimedActions.Contains(cast.Action) || cast.Finish < cast.Start || cast.Finish - cast.Start > TimeSpan.FromMinutes(1)) return;
        // Passage 49986/49987 is self-targeted but its packet locations are the two offset lanes.
        // Preserve those locations; the helper's transform remains on the black hole's centerline.
        if (cast.Action is not (50041 or >= 49985 and <= 49987 or >= 49995 and <= 49997) && _actors.TryGetValue(cast.Source, out var actor)) cast = cast with { Position = actor.Position };
        if (_seen.TryGetValue(cast.Source, out var seen) && seen.Action == cast.Action && Math.Abs((seen.Start - cast.Start).TotalSeconds) < 1)
        {
            if (authoritative && _casts.ContainsKey(cast.Source)) _casts[cast.Source] = cast;
            return;
        }
        _seen[cast.Source] = (cast.Action, cast.Start);
        _casts[cast.Source] = cast;
        _towerWave.Cast(cast);
        if (cast.Action is 50010 or 49369) _adds = true;
        if (cast.Action is 50029 or 49972) { _adds = false; _towerWave.Reset(); }
        if (cast.Action is 50032 or 50033 or 50045) { _wave = cast.Action; _waveStart = cast.Start; _waveUntil = cast.Finish.AddSeconds(1.2); }
        if (cast.Action == 50047) { _dimensionHits = 0; _dimensionUntil = cast.Finish.AddSeconds(8); }
        if (cast.Action == 50002)
        {
            _orbUntil = cast.Finish.AddSeconds(45); _consumedOrbs.Clear(); _orbAssignments.Clear(); _orbOrigins.Clear();
            foreach (var key in _tethers.Keys.Where(k => k.Id is 406 or 407).ToArray()) _tethers.Remove(key);
        }
        if (cast.Action == 49992) _chaseUntil = cast.Finish.AddSeconds(18);
        if (cast.Action == 50044)
        {
            if (cast.Start > _flareUntil) _flares.Clear();
            _flares.Add(cast.Target); _flareUntil = cast.Finish.AddSeconds(3);
        }
    }

    public void Cancel(ulong source, uint action) { if (_casts.TryGetValue(source, out var c) && c.Action == action) _casts.Remove(source); }
    public void RemoveActor(ulong source)
    {
        _actors.Remove(source); _casts.Remove(source);
        foreach (var key in _tethers.Keys.Where(k => k.Source == source).ToArray()) _tethers.Remove(key);
    }
    public void Icon(ulong target, uint icon, DateTime now)
    {
        if (icon == 721) { _towerWave.Icon(target, now); return; }
        if (icon is 701 or 702 && !_icons.Keys.Any(k => k.Icon is 701 or 702)) _chargeBearing = null;
        if (icon is 701 or 702 or 719 or 318 or 172 or 471) _icons[(icon, target)] = now.AddSeconds(icon == 719 ? 16 : 14);
        if (icon == 719) { _dimensionHits = 0; _dimensionUntil = now.AddSeconds(16); }
    }
    public void Tether(ulong source, ulong target, uint id, DateTime now)
    {
        if (id is not (284 or 404 or 405 or 406 or 407)) return;
        var key = (id, source);
        if (!_tethers.TryGetValue(key, out var previous) || previous.Target != target) _tethers[key] = (target, now);
    }
    public void Untether(ulong source, uint id) => _tethers.Remove((id, source));
    public void Status(ulong target, uint id, ushort param, ulong source, DateTime? until)
    {
        if (_actors.TryGetValue(target, out var actor))
            _actors[target] = actor with { Statuses = [.. actor.Statuses.Where(s => s.Id != id),
                .. until is { } expiry ? new ReplayStatus[] { new(id, param, source, expiry) } : []] };
        if (target == _local && id == 3523 && until == null) _freezeUntil = default;
    }
    public void Effect(ulong source, uint action, uint sequence, DateTime now)
    {
        if (sequence != 0 && !_effects.Add((action, sequence))) return;
        Cancel(source, action);
        _towerWave.Effect(source, action, now);
        if (action is 49983 or 49984) RemoveIcons(701, 702);
        if (action is 50034 or 50035 or 50046) { _wave = 0; _waveUntil = default; }
        if (action == 50048 && ++_dimensionHits >= 3) { _dimensionUntil = default; RemoveIcons(719); }
        if (action is 50006 or 50007) _consumedOrbs.Add(source);
        if (action == 50044) _freezeUntil = now.AddSeconds(2);
        if (action == 50031) _adds = false;
        Tick(now);
    }
    private void RemoveIcons(params uint[] ids)
    {
        foreach (var key in _icons.Keys.Where(k => ids.Contains(k.Icon)).ToArray()) _icons.Remove(key);
    }

    public EnuoPlan Plan(DateTime now)
    {
        Tick(now);
        var plan = new EnuoPlan { ArenaRadius = _adds ? 40 : 20 };
        AddCastHazards(plan, now);
        if (!_actors.TryGetValue(_local, out var me) || me.Dead) return plan;
        var role = Role;
        if (role < 0)
        {
            plan.Hints.Add("恩欧：需要完整且不重复的 MT/ST/H1/H2/D1–D4 分工；仅显示范围。");
            return plan;
        }
        if (_waveUntil > now)
        {
            var p = _wave == 50032 ? EnuoStrategy.Pairs(role) : EnuoStrategy.LightParty(role);
            plan.Goal = new(_wave == 50032 ? "视频：两两分摊" : "视频：东西四四分摊", p, _waveUntil);
            AddWaveHazards(plan, role, now);
        }
        WildCharge(plan, role, now);
        if (_dimensionUntil > now)
        {
            plan.Goal = new("零式波动：三连分摊", new(100, 0, 105), _dimensionUntil);
            plan.Hints.Add($"零式波动：留在分摊线内（{_dimensionHits}/3）。");
        }
        var melt = _casts.Values.Where(c => c.Action == 50042 && c.Target == _local).FirstOrDefault();
        if (melt != null) plan.Goal = new("核心熔毁：八方散开", EnuoStrategy.Spread(role), melt.Finish.AddSeconds(0.5));
        else if (!_adds && _casts.Values.Any(c => c.Action == 50040) && !_casts.Values.Any(c => c.Action is >= 49995 and <= 50001))
            plan.Goal = new("核心熔毁：中间集合", EnuoStrategy.Center, now.AddSeconds(1));
        var freezeCast = _casts.Values.FirstOrDefault(c => c.Action is 50043 or 50044);
        if (freezeCast != null || HasStatus(me, 3523, now) || _freezeUntil > now)
        {
            var flareTargets = _flares.OrderBy(id => Array.IndexOf(_party, id)).ToArray();
            var flareSlot = Array.IndexOf(flareTargets, _local);
            if (flareTargets.Length == 0 && role < 2) flareSlot = role;
            var destination = flareSlot >= 0 ? EnuoStrategy.Polar(flareSlot == 0 ? -MathF.PI / 4 : MathF.PI / 4, 18) : new Vector3(100, 0, 110);
            plan.Goal = new("深度冻结：双 T 北侧、人群南侧", destination, freezeCast?.Finish.AddSeconds(3) ?? now.AddSeconds(1));
            if (HasStatus(me, 3523, now) || _freezeUntil > now || freezeCast != null && freezeCast.Finish - now < TimeSpan.FromSeconds(0.5))
            {
                plan.KeepMoving = plan.BlockCasts = true;
                plan.Goal = plan.Goal with { Position = MovingPoint(me.Position, destination) };
                plan.Hints.Add("冷却：持续移动至状态消失。");
            }
            if (role < 2 && freezeCast != null && freezeCast.Finish - now < TimeSpan.FromSeconds(5))
                plan.Actions.Add(new(7531, _local, freezeCast.Finish, "冻结距离衰减减伤"));
        }
        else _freezeStep = null;
        Orbs(plan, role, now);
        Adds(plan, role, now);
        Chase(plan, role, now);
        if (HasStatus(me, 4562, now))
        {
            plan.HoldMovement = true; plan.KeepMoving = false; plan.Goal = null;
            plan.Hints.Add("热病：停步，等状态消失后再散开。");
        }
        foreach (var cast in _casts.Values.Where(c => c.Action is 50049 or 49972 or 50050))
            plan.Hints.Add(cast.Action == 50050 ? "至高无上：狂暴读条。" : "全屏伤害：准备减伤与治疗。");
        return plan;
    }

    private void AddCastHazards(EnuoPlan plan, DateTime now)
    {
        foreach (var c in _casts.Values)
        {
            GeometryTemplate? shape = c.Action switch
            {
                49977 => new CircleTemplate(40), 49978 => new DonutTemplate(40, 60),
                49979 => new CircleTemplate(12), 49980 => new DonutTemplate(6, 40),
                49985 or 49986 or 49987 => RayRectangleTemplate.Symmetric(16, 80),
                50001 or >= 49995 and <= 49997 => new CircleTemplate(7),
                49998 => Arc(10), 49999 => Arc(20), 50000 => Arc(30),
                50005 => new FanTemplate(40, MathF.PI / 2),
                50041 or 50042 => new CircleTemplate(5),
                50044 => new CircleTemplate(10), 49369 => new CircleTemplate(8),
                50017 => RayRectangleTemplate.Symmetric(4, 100),
                50021 => RayRectangleTemplate.Symmetric(8, 100),
                48475 => new CircleTemplate(6), _ => null
            };
            if (shape == null) continue;
            if (c.Action is 50042 or 50044 or 50021 && c.Target == _local) continue;
            var position = c.Position;
            var rotation = c.Rotation;
            if (c.Action is 50042 or 50044 && _actors.TryGetValue(c.Target, out var target)) position = target.Position;
            if (c.Action == 50021 && _actors.TryGetValue(c.Target, out target)) rotation = GeometryHelper.AngleTo(position, target.Position);
            var from = c.Action is >= 49995 and <= 49997 ? c.Finish.AddSeconds(2) : c.Finish;
            plan.Hazards.Add(new($"cast/{c.Source}/{c.Action}", shape, position, rotation, from, from.AddSeconds(0.6), c.Start));
        }
    }

    private void AddWaveHazards(EnuoPlan plan, int role, DateTime now)
    {
        var boss = _actors.Values.FirstOrDefault(a => a.Oid == 0x4DC1);
        if (boss == null || _wave == 50045) return;
        var myAnchor = _wave == 50032 ? EnuoStrategy.Pairs(role) : EnuoStrategy.LightParty(role);
        var others = Enumerable.Range(0, _wave == 50032 ? 4 : 2).Select(i => _wave == 50032 ? EnuoStrategy.Pairs(i) : EnuoStrategy.LightParty(i));
        foreach (var p in others.Where(p => p != myAnchor))
            plan.Hazards.Add(new($"wave/{p.X}/{p.Z}", new FanTemplate(60, (_wave == 50032 ? 60 : 100) * MathF.PI / 180), boss.Position,
                GeometryHelper.AngleTo(boss.Position, p), _waveUntil.AddSeconds(-1), _waveUntil, _waveStart));
    }

    private void WildCharge(EnuoPlan plan, int role, DateTime now)
    {
        var markers = _icons.Where(p => p.Key.Icon is 701 or 702).ToArray();
        if (markers.Length == 0) return;
        var single = markers.Any(p => p.Key.Icon == 702);
        var healer = single ? markers[0].Key.Target : _party[role % 2 + 2];
        if (!_actors.TryGetValue(healer, out var h)) return;
        if (h.Dead || !markers.Any(p => p.Key.Target == healer))
        { plan.Hints.Add("挡枪：本组点名未确认或目标已死亡，需要人工调整分摊。"); return; }
        var anchor = ChargeAnchor(plan.Hazards, role, single);
        if (anchor == null) { plan.Hints.Add("挡枪：未找到满足黑洞范围的站位，确认安全区。"); return; }
        // The charge comes inward along the marked healer's radial line. ARR orb transforms
        // remain at the boss until immediately before impact; they cannot identify the ball side.
        var boss = _actors.Values.FirstOrDefault(a => a.Oid == 0x4DC1)?.Position ?? EnuoStrategy.Center;
        var outward = Unit(anchor.Value - boss);
        var point = anchor.Value + outward * (role < 2 ? 2 : 0);
        // During boss donut, hold just inside the same safe sector; step out after its effect is observed.
        if (plan.Hazards.Any(a => a.Contains(point))) point = anchor.Value;
        if (!plan.Hazards.Any(a => a.Contains(point))) plan.Goal = new("回归波动：T 在球侧挡枪", point, markers.Min(p => p.Value));
        plan.Hints.Add("回归波动：球由外向内；T 在奶妈外侧，奶与 DPS 同线分摊。");
    }

    private void Orbs(EnuoPlan plan, int role, DateTime now)
    {
        if (now > _orbUntil) return;
        var all = _actors.Values.Where(a => a.Oid is 0x4DC5 or 0x4DC6 && !a.Dead).ToArray();
        var large = all.Where(a => a.Oid == 0x4DC6).ToArray();
        if (large.Length == 2)
        {
            var origin = EnuoStrategy.Angle((Origin(large[0]) + Origin(large[1])) / 2);
            foreach (uint type in new uint[] { 406, 407 })
                if (!_orbAssignments.ContainsKey(type))
                {
                    var ids = _tethers.Where(p => p.Key.Id == type).Select(p => p.Key.Source).ToHashSet();
                    var assignment = EnuoStrategy.AssignOrbs(all.Where(a => ids.Contains(a.Id)).Select(a => a with { Position = Origin(a) }), origin);
                    if (assignment.Length == 4) _orbAssignments[type] = assignment;
                }
        }
        var myOrb = 0UL;
        if (_orbAssignments.TryGetValue(407, out var yellow) && _orbAssignments.TryGetValue(406, out var purple))
            myOrb = !_consumedOrbs.Contains(yellow[role / 2]) ? yellow[role / 2] : !_consumedOrbs.Contains(purple[role / 2]) ? purple[role / 2] : 0;
        var me = _actors[_local];
        var vulnerable = HasStatus(me, 2941, now);
        var partner = _actors.GetValueOrDefault(_party[role ^ 1]);
        foreach (var orb in all.Where(a => !_consumedOrbs.Contains(a.Id)))
        {
            var mine = orb.Id == myOrb;
            var ready = mine && !vulnerable && partner is { Dead: false } && !HasStatus(partner, 2941, now)
                && Vector3.DistanceSquared(me.Position, partner.Position) < 9;
            if (mine)
            {
                var dir = Unit(orb.Position - EnuoStrategy.Center);
                plan.Goal = new(ready ? "双人撞球" : "撞球：等搭档与易伤", ready ? orb.Position : orb.Position - dir * 6.5f, now.AddSeconds(1));
                if (role < 2) plan.Actions.Add(new(7531, _local, now.AddSeconds(1), "双 T 大球减伤"));
                if (!ready) plan.Hints.Add("撞球：与搭档集合，易伤消失后一起碰球。");
            }
            if (!ready)
                plan.Hazards.Add(new($"orb/{orb.Id}", new CircleTemplate(orb.Oid == 0x4DC6 ? 6 : 5), orb.Position, 0, now, now.AddSeconds(0.3), now));
        }
        Vector3 Origin(ReplayActorState a) => _orbOrigins.GetValueOrDefault(a.Id, a.Position);
    }

    private void Adds(EnuoPlan plan, int role, DateTime now)
    {
        if (!_adds) return;
        var towerPending = _towerWave.Apply(plan, role, _party, now);
        var own = _tethers.Where(p => p.Key.Id == 284 && p.Value.Target == _local).Select(p => _actors.GetValueOrDefault(p.Key.Source))
            .Where(a => a is { Dead: false, Targetable: true }).OrderBy(a => a!.Id).FirstOrDefault();
        var shadows = _actors.Values.Where(a => a.Oid is >= 0x4DC8 and <= 0x4DCA && !a.Dead).ToArray();
        plan.ForbiddenTargets = shadows.Where(a => a.Id != own?.Id).Select(a => a.Id).ToArray();
        plan.Target = own?.Id ?? _actors.Values.Where(a => a.Oid is 0x4DC7 or 0x4DCB && a.Targetable && !a.Dead).OrderBy(a => a.Oid).Select(a => a.Id).FirstOrDefault();
        if (own is not null)
        {
            if (plan.Goal == null && !towerPending) plan.Goal = new("击杀自己的连线小怪", own.Position + Unit(own.Position - EnuoStrategy.Center) * 3, now.AddSeconds(1), 1);
            if (role < 2 && _casts.TryGetValue(own.Id, out var interrupt) && interrupt.Action == 50018 && interrupt.Finish > now)
                plan.Actions.Add(new(7538, own.Id, interrupt.Finish, "吸血触：插言"));
            if (role < 2 && _casts.TryGetValue(own.Id, out var buster) && buster.Action == 50020)
                plan.Actions.Add(new(7531, _local, buster.Finish, "高压波动减伤"));
        }
        var me = _actors[_local];
        if (role is 2 or 3 && HasStatus(me, 3943, now)) plan.Actions.Add(new(7568, _local, now.AddSeconds(1), "驱散自己的疾病"));
        var gaze = _casts.Values.Where(c => c.Action is 50022 or 50023 && Vector3.DistanceSquared(c.Position, me.Position) < 400
            && c.Finish - now < TimeSpan.FromSeconds(1.8)).FirstOrDefault();
        if (gaze != null)
        {
            plan.Facing = GeometryHelper.AngleTo(gaze.Position, me.Position);
            plan.HoldActions = plan.BlockCasts = true;
            plan.Hints.Add("恶魔之瞳：背对，暂停攻击。");
        }
        var knockback = _casts.Values.FirstOrDefault(c => c.Action == 49982);
        if (knockback != null)
        {
            var point = EnuoStrategy.Polar(EnuoStrategy.Angle(me.Position), 9.5f);
            plan.Goal = new("击退：蓝圈外侧", point, knockback.Finish);
            if (knockback.Finish - now < TimeSpan.FromSeconds(5))
                plan.Actions.Add(new(me.ClassJob is 24 or 28 or 33 or 40 or 25 or 27 or 35 or 42 ? 7559u : 7548u, _local, knockback.Finish, "虚无大冲击防击退"));
        }
    }

    private void Chase(EnuoPlan plan, int role, DateTime now)
    {
        if (now > _chaseUntil) return;
        foreach (var a in _actors.Values.Where(a => a.Oid == 0x4EB5 && !a.Dead))
            plan.Hazards.Add(new($"chase/{a.Id}", new CircleTemplate(6), a.Position, 0, now, now.AddSeconds(0.3), now));
        var initial = _tethers.FirstOrDefault(p => p.Key.Id == 404 && p.Value.Target == _local);
        var relay = _tethers.FirstOrDefault(p => p.Key.Id == 405 && p.Value.Target == _local);
        var first = initial.Key.Source != 0;
        if (!first && relay.Key.Source == 0) return;
        var me = _actors[_local];
        var chaser = first ? _actors.GetValueOrDefault(initial.Key.Source) : _actors.GetValueOrDefault(relay.Key.Source);
        if (chaser == null) return;
        var starting = _casts.Values.FirstOrDefault(c => c.Action == 49992);
        if (starting != null && starting.Finish > now)
            plan.Goal = new("追踪：黑球顺时针一标点待机", EnuoStrategy.Polar(EnuoStrategy.Angle(chaser.Position) + MathF.PI / 4, 7), starting.Finish);
        else if (!first)
        {
            plan.Goal = new("追踪：到前一人顺时针前方接力", EnuoStrategy.Polar(EnuoStrategy.Angle(chaser.Position) + 0.65f, 17), now.AddSeconds(1));
            plan.BlockCasts = true;
        }
        else
        {
            plan.Goal = new("追踪：顺时针沿场边接力", EnuoStrategy.Polar(EnuoStrategy.Angle(me.Position) + 0.35f, 17), now.AddSeconds(1));
            plan.KeepMoving = plan.BlockCasts = true;
            plan.Actions.Add(new(3, _local, now.AddSeconds(1), "追踪冲刺"));
        }
    }

    private Vector3? ChargeAnchor(IEnumerable<EnuoHazard> hazards, int role, bool single)
    {
        var dangers = hazards.Select(h => h.Shape.Resolve(h.Position, h.Rotation, null)).ToArray();
        var edge = _casts.Values.FirstOrDefault(c => c.Action is 49977 or 49978);
        var desired = edge != null ? EnuoStrategy.Angle(edge.Position) + (edge.Action == 49977 ? MathF.PI : 0) : _chargeBearing ?? MathF.PI;
        var radius = _casts.Values.Any(c => c.Action == 49979) ? 13.5f : _casts.Values.Any(c => c.Action == 49980) ? 4.8f : 6f;
        var separation = single ? 0 : MathF.PI / 4;
        float? best = null; var score = float.MaxValue;
        for (var i = 0; i < 72; i++)
        {
            var bearing = i * MathF.Tau / 72;
            if (new[] { -separation, separation }.Any(offset => dangers.Any(d => d.Contains(EnuoStrategy.Polar(bearing + offset, radius), 0.7f)))) continue;
            var difference = MathF.Abs(MathF.Atan2(MathF.Sin(bearing - desired), MathF.Cos(bearing - desired)));
            if (difference < score) { score = difference; best = bearing; }
        }
        if (best == null) return null;
        _chargeBearing = best;
        // Viewed from the safe sector towards the boss: left is clockwise from the radial bearing.
        return EnuoStrategy.Polar(best.Value + (role % 2 == 0 ? separation : -separation), radius);
    }

    private Vector3 MovingPoint(Vector3 position, Vector3 anchor)
    {
        if (Vector3.DistanceSquared(position, anchor) > 6.25f) return anchor;
        if (_freezeStep == null || Vector3.DistanceSquared(position, _freezeStep.Value) < 0.16f)
            _freezeStep = anchor + new Vector3(position.X <= anchor.X ? 2 : -2, 0, 0);
        return _freezeStep.Value;
    }
    private static Vector3 Unit(Vector3 v) { v.Y = 0; return v.LengthSquared() > 0.0001f ? Vector3.Normalize(v) : Vector3.Zero; }
    private static bool HasStatus(ReplayActorState actor, uint id, DateTime now) => actor.Statuses.Any(s => s.Id == id && s.ExpiresAt > now);
    private static GeometryTemplate Arc(float halfAngle)
    {
        var points = new List<Vector2>();
        for (var i = 0; i <= 24; i++) { var a = (-halfAngle + 2 * halfAngle * i / 24) * MathF.PI / 180; points.Add(new(19 * MathF.Sin(a), -19 * MathF.Cos(a))); }
        for (var i = 24; i >= 0; i--) { var a = (-halfAngle + 2 * halfAngle * i / 24) * MathF.PI / 180; points.Add(new(17 * MathF.Sin(a), -17 * MathF.Cos(a))); }
        return new LocalPolygonTemplate(points);
    }
}
