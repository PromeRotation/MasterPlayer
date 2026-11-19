using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class PilgrimsBossRegression
{
    private static readonly DateTime Start = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Pilgrims bosses arena edges and collision islands", Arenas);
        run("Pilgrims bosses PT10 telegraphs and effect deduplication", Leafman);
        run("Pilgrims bosses PT20 knockback bursts and growing roots", Emulation);
        run("Pilgrims bosses PT40 consecutive knockback direction", Naivety);
        run("Pilgrims bosses PT50 quicksand shrink and chase", Ogbunabali);
        run("Pilgrims bosses PT60 marching and actual rotating pulses", Maliktender);
        run("Pilgrims bosses PT70 eight swipes and ordered sphere halos", Zeal);
        run("Pilgrims bosses PT80 lightning order debuff reversal and hold", Profanity);
        run("Pilgrims bosses PT90 directional landing in diamond", Malacoda);
        run("Pilgrims bosses PT99 color sequence pillars chains and exaflares", EminentGrief);
        run("Pilgrims bosses temporal solver crosses resolved wave", TemporalSolver);
        run("Pilgrims bosses cancellation wipe and fresh pull", Lifecycle);
        run("Pilgrims bosses interleaved snapshots late effects and preparation", InputOrdering);
    }

    private static ReplayActorState Actor(ulong id, uint oid, Vector3 position, bool combat = true)
        => new() { Id = id, EntityId = (uint)id, Oid = oid, Position = position, InCombat = combat, Targetable = true, Hp = 1000, MaxHp = 1000 };
    private static void Begin(PtBossEncounter e, params ReplayActorState[] more)
        => e.Synchronize([Actor(1, e.BossOid, e.Center), Actor(100, 0, e.Center) with { Kind = 1, ClassJob = 31 }, .. more], Start);
    private static PtBossEncounter.Cast Cast(uint action, Vector3 pos, double hit, ulong source = 10, float rotation = 0)
        => new(source, action, pos, rotation, Start, Start.AddSeconds(hit), 100, pos);
    private static void Effect(PtBossEncounter e, uint action, uint sequence, double seconds, Vector3? pos = null, ulong source = 10, float? rotation = null)
        => e.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = action, GlobalSequence = sequence, EffectPosition = pos, SourcePosition = pos, SourceRotation = rotation }, Start.AddSeconds(seconds));
    private static void Status(PtBossEncounter e, ulong actor, uint id, ushort param, double seconds = 0, float duration = 30)
        => e.Apply(new LogSystemStatusAddEvent { TargetId = actor, StatusId = id, Param = param, Duration = duration }, Start.AddSeconds(seconds));
    private static void Map(PtBossEncounter e, uint index, uint state)
        => e.Apply(new LogSystemMapEffectEvent { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16) }, Start);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static PtBossEncounter[] Encounters() => [new Pt10Encounter(), new Pt20Encounter(), new Pt40Encounter(), new Pt50Encounter(), new Pt60Encounter(), new Pt70Encounter(), new Pt80Encounter(), new Pt90Encounter(), new Pt99Encounter()];

    private static void Arenas()
    {
        foreach (var e in Encounters())
        {
            Begin(e);
            Check(e.Boundary.Contains(e.Center), $"PT{e.Floor}: center is inside physical arena");
            Check(!e.Boundary.Contains(e.Center + new Vector3(50, 0, 0)), $"PT{e.Floor}: distant point is outside arena");
            Check(PtBossCatalog.Owns(e.BossOid, e.Floor == 99 ? 1290u : (uint)(1280 + e.Floor / 10)), "boss ownership matches actual territory");
            Check(!PtBossCatalog.Owns(e.BossOid, 0), "identical actors outside Pilgrims must retain their original module");
        }
        var e20 = new Pt20Encounter();
        Check(e20.Boundary.Contains(new(-285.3f, 0, -300)) && !e20.Boundary.Contains(new(-285.28f, 0, -300)), "PT20 preserves the 14.709m arena edge");
        var e90 = new Pt90Encounter();
        Check(e90.Boundary.Contains(new(-272, 0, -300)) && !e90.Boundary.Contains(new(-280, 0, -280)), "PT90 is a 45-degree square, not a circle or axis-aligned square");
        var e99 = new Pt99Encounter();
        Check(e99.Boundary.Contains(new(-580, 0, -300)) && !e99.Boundary.Contains(new(-579.9f, 0, -300)), "PT99 cannot navigate past the physical 40x30m floor");
        Check(PtArenaData.SandIslands.Length == 7 && PtArenaData.LightPuddles.Length == 5 && PtArenaData.DarkPuddles.Length == 5, "all collision contours survive export");
        Check(PtArenaData.LightPuddles.All(s => s.GetContours().Single().Count == 23), "light puddles retain all 23 collision vertices each");
        Check(PtArenaData.SandFloor.GetBounds() == new GeometryBounds(-317.82f, -317.95f, -282.41f, -280.76f), "PT50 starts with its irregular physical edge");
    }

    private static void Leafman()
    {
        var e = new Pt10Encounter(); Begin(e);
        var p = e.Center;
        Effect(e, 44058, 1, 0, p);
        Effect(e, 44058, 2, 1, p + new Vector3(5, 0, 0));
        var before = e.Collect(Start, 100).OrderBy(a => a.From).ToArray();
        Check(before.Length == 2 && Math.Abs((before[1].From - before[0].From).TotalSeconds - .7) < .001, "Leafmash telegraphs retain the -0.3s per queued jump rule");
        e.StartCast(Cast(44057, p, 8.9));
        e.StartCast(Cast(44057, p + Vector3.One, 8.91));
        var actual = e.Collect(Start, 100).Single(a => a.Action == 44057);
        Check(actual.Position == p && actual.Geometry.Contains(p + new Vector3(14.9f, 0, 0)) && !actual.Geometry.Contains(p + new Vector3(15.1f, 0, 0)), "real Action44057 is R15 and repeated observations retain packet pose");
        Effect(e, 44057, 3, 8.9, p); Effect(e, 44057, 3, 8.9, p);
        Check(e.Collect(Start.AddSeconds(8.9), 100).Count == 1, "one effect consumes exactly one jump, even with duplicate targets");
    }

    private static void Emulation()
    {
        var e = new Pt20Encounter(); Begin(e);
        for (ushort i = 0; i < 4; ++i) Status(e, 1, 2056, (ushort)(0x395 + i), i);
        var bursts = e.Collect(Start, 100).OrderBy(a => a.From).ToArray();
        Check(bursts.Length == 4 && bursts[0].Position.X == -293.65f, "Touchdown preserves the asymmetric first quadrant");
        for (var i = 1; i < 4; ++i) Check(Math.Abs((bursts[i].From - bursts[i - 1].From).TotalSeconds - 1.3) < .001, "Burst sequence uses previous hit+1.3s, not status arrival+delay");
        e.StartCast(Cast(43455, e.Center, 14.5));
        var landing = e.Collect(Start, 100).Single(a => a.Key == "touchdown/landing");
        Check(landing.Geometry.Contains(e.Center + new Vector3(10, 0, 0)), "Touchdown cannot knock outside the real boundary");
        e.StartCast(Cast(43461, e.Center + new Vector3(0, 0, 13), 3));
        Status(e, 10, 1909, 3);
        var roots = e.Collect(Start, 100).Where(a => a.Key.StartsWith("roots/")).ToArray();
        Check(roots.Length == 4 && roots.All(a => a.Obstacle), "growing roots are physical obstacles, separate from future full crosses");
        for (uint i = 1; i <= 29; ++i) Effect(e, 43462, 100 + i, 5);
        Check(e.Collect(Start.AddSeconds(5), 100).Any(a => a.Key.StartsWith("roots/")), "roots remain through the 29th actual effect");
        Effect(e, 43462, 130, 5); Effect(e, 43462, 130, 5);
        Check(!e.Collect(Start.AddSeconds(5), 100).Any(a => a.Key.StartsWith("roots/") || a.Key.StartsWith("embrace/")), "the 30th effect clears both root layers once");
    }

    private static void Naivety()
    {
        var e = new Pt40Encounter(); Begin(e);
        e.StartCast(Cast(42128, e.Center + new Vector3(-10, 0, 0), 10, 10));
        e.StartCast(Cast(42128, e.Center + new Vector3(10, 0, 0), 13, 11));
        var a = e.Collect(Start, 100).Single();
        var template = (PtKnockbackTemplate)a.Shape;
        Check(template.LandingAllowed != null && template.LandingAllowed.Contains(e.Center + new Vector3(11, 0, 0)), "first knockback must face the next source");
        Check(!template.LandingAllowed!.Contains(e.Center + new Vector3(-11, 0, 0)), "wrong side cannot pass the chained-knockback constraint");
        Effect(e, 42128, 1, 10, source: 10);
        Check(((PtKnockbackTemplate)e.Collect(Start.AddSeconds(10), 100).Single().Shape).Origin.X == -590, "second source survives the first effect");
        e.Reset(); Begin(e);
        e.StartCast(Cast(42128, e.Center + new Vector3(-10, 0, 0), 10, 10));
        e.StartCast(Cast(42128, e.Center + new Vector3(10, 0, 0), 13, 11));
        Status(e, 100, 160, 0, duration: 11);
        Check(((PtKnockbackTemplate)e.Collect(Start, 100).Single().Shape).Origin.X == -590,
            "immunity expiring between two knockbacks must not hide the second prepositioning constraint");
    }

    private static void Ogbunabali()
    {
        var e = new Pt50Encounter(); Begin(e);
        e.StartCast(Cast(43531, e.Center, 3)); Status(e, 100, 567, 0, duration: 10);
        var sand = e.Collect(Start, 100).Single(a => a.Key == "sand/leave");
        Check(sand.From == Start.AddSeconds(9) && PtArenaData.SandIslands.All(s => !sand.Geometry.Contains(s.GetContours().Single()[0], -.001f)), "quicksand uses exact land subtraction and per-player sinking expiration");
        e.StartCast(Cast(43538, e.Center, 8));
        Check(e.Collect(Start.AddSeconds(6.1), 100).Any(a => a.Key == "sand/enter"), "last two seconds reverse the quicksand requirement before knockback");
        Status(e, 100, 4625, 0);
        Check(e.Collect(Start.AddSeconds(6.1), 100).Any(a => a.Key == "sand/enter") && e.Collect(Start.AddSeconds(6.1), 100).Any(a => a.Key == "sand/leave"),
            "Breaker of Wind requires staying in sand through the hit while preserving the sinking deadline");
        e.Apply(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 4625 }, Start.AddSeconds(6.2));
        Check(e.Collect(Start.AddSeconds(6.2), 100).Any(a => a.Key == "sand/enter"), "losing duty immunity requires sand again");
        Map(e, 1, 0x00020001);
        var output = e.Collect(Start, 100);
        Check(output.Any(a => a.Key == "wind/hole" && a.Obstacle && a.Geometry.Contains(e.Center)), "Windraiser creates the real center hole");
        Check(!e.Boundary.Contains(e.Center + new Vector3(15.1f, 0, 0)), "Windraiser shrinks the navigable edge to R15");
        e.StartCast(Cast(43534, e.Center, 3));
        var chase = e.Collect(Start, 100).Single(a => a.Key == "pit/chase");
        Check(chase.Geometry.Contains(e.Center + new Vector3(5.9f, 0, 0)) && !chase.Geometry.Contains(e.Center + new Vector3(6.1f, 0, 0)), "all Pit Ambush hits retain the actual R6 footprint");
        for (uint i = 1; i <= 4; ++i) Effect(e, i == 1 ? 43534u : i == 4 ? 43536u : 43535u, i, 3 + 2.1 * (i - 1), e.Center);
        Check(!e.Collect(Start.AddSeconds(9.3), 100).Any(a => a.Key == "pit/chase"), "four actual jumps release the chase");
    }

    private static void Maliktender()
    {
        var e = new Pt60Encounter(); Begin(e);
        var p = e.Center + new Vector3(-15, 0, -15);
        Check(Pt60Encounter.PredictMarch(p, true, 2, e.Center) == e.Center + new Vector3(5, 0, -15), "outer ring marches clockwise by two discrete tiles");
        Check(Pt60Encounter.PredictMarch(e.Center + new Vector3(-5, 0, -5), false, 1, e.Center) == e.Center + new Vector3(-5, 0, 5), "inner ring wraps counterclockwise independently");
        e.StartCast(Cast(44862, e.Center, 14)); Map(e, 0, 0x00020001);
        for (var i = 0; i < 8; ++i) e.StartCast(Cast(44864, e.Center + new Vector3(i % 4 * 10 - 15, 0, i / 4 * 30 - 15), 12.7, (ulong)(20 + i)));
        Check(e.Collect(Start, 100).Count(a => a.Key.StartsWith("march/")) == 8, "eight casters and MapEffect produce the final tile hazards before the short casts");
        Effect(e, 44861, 1, 16.2);
        e.StartCast(Cast(44868, e.Center, 5.3, 1));
        var rotating = e.Collect(Start, 100).Where(a => a.Key.StartsWith("rotate/") && !a.Safe).ToArray();
        Check(rotating.Length == 20 && Math.Abs((rotating.Min(a => a.From) - Start).TotalSeconds - 5.2) < .000001,
            $"two opposite ten-hit sequences cover cast finish+0.4s with a 0.2s lead (count={rotating.Length}, first={rotating.Min(a => a.From):O})");
        Effect(e, 44909, 2, 5.6, e.Center, 30, 0);
        Effect(e, 44909, 2, 5.6, e.Center, 30, 0);
        var next = e.Collect(Start.AddSeconds(5.6), 100).First(a => a.Key == "rotate/0/0");
        Check(Math.Abs(next.Rotation + MathF.PI / 3) < .001 && Math.Abs((next.From - Start).TotalSeconds - 6.5) < .001, "real rotating hit advances once and reanchors the next pulse to actual time+1.1s");
    }

    private static void Zeal()
    {
        var e = new Pt70Encounter(); Begin(e);
        for (var i = 0; i < 8; ++i) e.StartCast(Cast(43437, e.Center, 1 + .6 * i, (ulong)(10 + i), i * MathF.PI / 2));
        var swipes = e.Collect(Start, 100).OrderBy(a => a.From).ToArray();
        Check(swipes.Length == 8 && Math.Abs((swipes[7].From - swipes[0].From).TotalSeconds - 14) < .001, "eight memory telegraphs preserve the two-second hit cadence");
        Effect(e, 43434, 1, 9.2); Effect(e, 43434, 1, 9.2);
        Check(e.Collect(Start.AddSeconds(9.2), 100).Count(a => a.Key.StartsWith("swipe/")) == 7, "one real swipe clears only its own wave");
        e.Reset(); Begin(e); e.StartCast(Cast(43411, e.Center, 3, 1));
        e.Synchronize([Actor(1, e.BossOid, e.Center), Actor(25, 0x484D, e.Center + Vector3.UnitX), Actor(20, 0x484D, e.Center - Vector3.UnitX)], Start);
        var halos = e.Collect(Start, 100).OrderBy(a => a.From).ToArray();
        Check(halos[0].Owner == 20 && Math.Abs((halos[1].From - halos[0].From).TotalSeconds - 2.5) < .001, "sphere instance order determines consecutive halos even when observed in reverse order");
        Check(!halos[0].NavigationOnly && halos[1].NavigationOnly, "only the next halo may be previewed; future halos must remain in navigation");
        Check(halos.All(a => !a.PreviewOnly && a.Shape is DonutTemplate { InnerRadius: 3, OuterRadius: 20 }), "preview filtering must retain exact R3-20 navigation geometry");
        e.StartCast(Cast(43417, halos[0].Position, 11.4, 20));
        var locked = e.Collect(Start, 100).Where(a => a.Name == "Brutal Halo").OrderBy(a => a.From).ToArray();
        Check(locked.Length == 2 && locked.Count(a => !a.NavigationOnly) == 1 && locked[0].Owner == 20,
            "the actual cast must replace its predicted halo without drawing the next halo too");
        Effect(e, 43417, 2, 11.4, source: 20); Effect(e, 43417, 2, 11.4, source: 20);
        var remaining = e.Collect(Start.AddSeconds(11.4), 100).Single();
        Check(remaining.Owner == 25 && !remaining.NavigationOnly, "one actual effect must reveal the next halo exactly once");
        e.Cancel(25);
        Check(e.Collect(Start.AddSeconds(11.4), 100).Count == 0, "cancel must remove the final halo from navigation and previews");
        e.Reset(); Begin(e); e.StartCast(Cast(43411, e.Center, 3, 1));
        e.Synchronize([Actor(1, e.BossOid, e.Center), Actor(20, 0x484D, e.Center - Vector3.UnitX), Actor(25, 0x484D, e.Center + Vector3.UnitX)], Start);
        var afterExpiry = e.Collect(Start.AddSeconds(11.6), 100).Single();
        Check(afterExpiry.Owner == 25 && !afterExpiry.NavigationOnly, "an expired halo must not suppress the next preview when its effect is missing");
        var sector = new PtDonutSectorTemplate(5, 10, MathF.PI / 2).Resolve(e.Center, 0, null);
        Check(sector.Contains(e.Center + new Vector3(0, 0, 7)) && !sector.Contains(e.Center + new Vector3(0, 0, 4.9f)) && !sector.Contains(e.Center + new Vector3(7, 0, 0)), "ring sectors preserve the inner safe circle and 90-degree angle");
    }

    private static void Profanity()
    {
        var e = new Pt80Encounter();
        Begin(e, Enumerable.Range(0, 9).Select(i => Actor((ulong)(20 + i), 0x485E, e.Center + new Vector3(i - 4, 0, i % 2 * 10 - 5))).ToArray());
        var lightning = e.Collect(Start, 100).Where(a => a.Key.StartsWith("lightning/")).OrderBy(a => a.From).ToArray();
        Check(lightning.Length == 9 && lightning[0].Position.X == e.Center.X + 4 && lightning[^1].Position.X == e.Center.X - 4, "nine balls form a descending-instance-ID chain ending at the oldest ball");
        Check(Math.Abs((lightning[1].From - lightning[0].From).TotalSeconds - .4) < .001 && lightning[^1].Shape is CircleTemplate { Radius: 30 }, "charge cadence and R30 final explosion remain separate");
        e.StartCast(Cast(43473, e.Center, 6)); Status(e, 100, 4518, 0);
        var required = e.Collect(Start, 100).Single(a => a.Key == "waul/required");
        Check(!required.Geometry.Contains(e.Center + new Vector3(0, 0, 5)) && required.Geometry.Contains(e.Center + new Vector3(0, 0, -5)), "doomed perspective must enter the original cone, rather than dodge it");
        Check(e.Collect(Start, 101).Any(a => a.Key == "waul" && !a.Safe), "switching perspective preserves the normal cone for another player");
        Status(e, 100, 4519, 6);
        Check(e.HoldMovement(Start, 100) && !e.HoldMovement(Start, 101), "six movement stacks hold only the affected player's movement");
        e.Apply(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 4519 }, Start);
        Check(!e.HoldMovement(Start, 100), "status loss releases the movement hold");
    }

    private static void Malacoda()
    {
        var e = new Pt90Encounter(); Begin(e); e.StartCast(Cast(44266, e.Center, 5));
        var landing = (PtKnockbackTemplate)e.Collect(Start, 100).Single().Shape;
        var p = e.Center + new Vector3(0, 0, -20);
        Check(landing.Landing(p) == p + new Vector3(0, 0, 30), "Skinflayer is a fixed forward displacement, never radial");
        Check(!landing.Resolve(e.Center, 0, null).Contains(p) && landing.Resolve(e.Center, 0, null).Contains(e.Center), "knockback checks landing against the diamond edge");
        Status(e, 100, 160, 0);
        Check(e.Collect(Start, 100).Count == 0, "valid knockback immunity removes the positioning constraint");
    }

    private static void EminentGrief()
    {
        var e = new Pt99Encounter(); Begin(e, Actor(2, 0x48EB, e.Center));
        e.StartCast(Cast(44088, e.Center, 7, 1)); e.StartCast(Cast(44093, e.Center, 12, 3));
        Check(e.Collect(Start, 100).Any(a => a.Key == "color/goal" && a.Name == "获取光之复仇"), "first drain requires light before its own deadline");
        Check(e.Collect(Start, 100).Single(a => a.Key == "color/deadline").From == Start.AddSeconds(6), "drain color is mandatory one second before resolution");
        Effect(e, 44088, 1, 7, source: 1);
        Check(e.Collect(Start.AddSeconds(7), 100).Any(a => a.Key == "color/goal" && a.Name == "获取暗之复仇"), "the second drain requires dark after the first actual effect");
        Map(e, 3, 0x00020001); Map(e, 3, 0x00020001);
        Check(e.Collect(Start, 100).Count(a => a.Key.StartsWith("pillar/")) == 1, "duplicate env packets must not duplicate pillars");
        var pillar = e.Collect(Start, 100).Single(a => a.Key == "pillar/3");
        Check(pillar.Obstacle && pillar.Geometry.Contains(new(-593, 0, -300)), "pillar collision is a hard arena obstacle");
        Map(e, 0, 0x00080004);
        Check(!e.Collect(Start, 100).Any(a => a.Key.StartsWith("pillar/")), "pillar reset removes every pillar");
        e.StartCast(Cast(44064, e.Center, 6));
        Check(!e.HoldMovement(Start.AddSeconds(5), 100) && e.HoldMovement(Start.AddSeconds(6.1), 100), "chains allow prepositioning but hold at the actual resolution");
        Status(e, 100, 4562, 0, 6.1, 2);
        e.Apply(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 4562 }, Start.AddSeconds(7));
        Check(!e.HoldMovement(Start.AddSeconds(7), 100), "chains release independently for the affected perspective");
        e.Reset(); Begin(e);
        Effect(e, 44074, 1, 0);
        e.Synchronize([Actor(1, e.BossOid, e.Center), Actor(50, 0x1EBE70, e.Center)], Start);
        e.StartCast(Cast(44079, e.Center, 7));
        var blaze = e.Collect(Start, 100).Where(a => a.Key.StartsWith("blaze/")).ToArray();
        Check(blaze.Length == 10 && blaze.All(a => a.Shape is CircleTemplate { Radius: 5 }), "one crystal emits two independent R5 lines, five steps per direction");
        Effect(e, 44079, 2, 7.2, e.Center); Effect(e, 44079, 2, 7.2, e.Center);
        var after = e.Collect(Start.AddSeconds(7.2), 100).Where(a => a.Key.StartsWith("blaze/")).ToArray();
        Check(after.Length == 8 && after.Min(a => a.From) == Start.AddSeconds(8), "first explosion advances both directions once using observed timing");
        Check(after.Any(a => a.Position.X == -604) && after.Any(a => a.Position.X == -596), "next exaflares move four meters in both directions");
        Effect(e, 44082, 3, 10, e.Center);
        Check(Math.Abs((e.Collect(Start.AddSeconds(10), 100).Single(a => a.Key == "bounds/end").From - Start).TotalSeconds - 21.5) < .000001,
            "central R8 uses documented 11.7s actual delay, not BMR's 6s planning shortcut");
    }

    private static void InputOrdering()
    {
        var roots = new Pt20Encounter(); Begin(roots);
        Status(roots, 1, 2056, 0x395);
        roots.Synchronize([Actor(1, roots.BossOid, roots.Center) with { Statuses = [new(2056, 0x396, 0, Start.AddSeconds(30))] }], Start.AddSeconds(.6));
        Status(roots, 1, 2056, 0x395, .7);
        roots.Synchronize([Actor(1, roots.BossOid, roots.Center) with { Statuses = [new(2056, 0x396, 0, Start.AddSeconds(30))] }], Start.AddSeconds(1.3));
        roots.Synchronize([Actor(1, roots.BossOid, roots.Center)], Start.AddSeconds(2));
        roots.Synchronize([Actor(1, roots.BossOid, roots.Center) with { Statuses = [new(2056, 0x396, 0, Start.AddSeconds(30))] }], Start.AddSeconds(2.1));
        Check(roots.Collect(Start.AddSeconds(2.1), 100).Count == 2, "status events and stale/missing snapshots must not append duplicate bursts");

        var swipe = new Pt70Encounter(); Begin(swipe);
        swipe.StartCast(Cast(43437, swipe.Center, 1)); swipe.StartCast(Cast(43437, swipe.Center, 1.6, 11));
        swipe.Collect(Start.AddSeconds(9.4), 100);
        Effect(swipe, 43434, 1, 9.4);
        Check(swipe.Collect(Start.AddSeconds(9.4), 100).Single().From > Start.AddSeconds(10), "late first hit must retain the following swipe");

        var grief = new Pt99Encounter(); Begin(grief);
        Effect(grief, 44074, 1, 0);
        grief.StartCast(Cast(44079, grief.Center, 7));
        grief.Synchronize([Actor(1, grief.BossOid, grief.Center), Actor(0x100000032, 0x1EBE70, grief.Center) with { EntityId = 0xE0000000, GameObjectId = 0x100000032 }], Start.AddSeconds(.1));
        Check(grief.Collect(Start, 100).Count(a => a.Key.StartsWith("blaze/")) == 10, "late event-object snapshot pairs the pending crystal cast");
        grief.Reset();
        grief.Synchronize([Actor(1, grief.BossOid, grief.Center, false), Actor(2, 0x48EB, grief.Center, false),
            Actor(100, 0, grief.Center, false) with { Kind = 1 }, Actor(101, 0, grief.Center, false) with { Kind = 1, Statuses = [new(4560, 0, 0, Start.AddSeconds(30))] }], Start);
        Check(grief.Preparing && !grief.Active && grief.Collect(Start, 100).Count(a => a.Key.StartsWith("puddle/")) == 10, "prepull displays both sets of exact water contours");
        Check(grief.Hints(Start, 100).Any(h => h.Contains("获取暗色")), "prepull assigns the underrepresented color rather than always choosing light");
    }

    private static void TemporalSolver()
    {
        var e = new Pt70Encounter(); Begin(e);
        // Two opposed half-floor rectangles: stand in the later one until the
        // first resolves, then cross before the second. A union cannot solve it.
        e.StartCast(Cast(43407, e.Center + new Vector3(-5, 0, -5), 1.5, 10, MathF.PI / 2));
        e.StartCast(Cast(43408, e.Center + new Vector3(-5, 0, 0), 2.5, 11, MathF.PI / 2));
        using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
        manager.SetArenaBoundary(e.Boundary);
        foreach (var a in e.Collect(Start, 100)) manager.AddDangerArea(a.Shape, new StaticTransform(a.Position, a.Rotation), [a.Window]);
        var player = e.Center + new Vector3(-2, 0, 3);
        var goal = e.Center + new Vector3(-2, 0, -3);
        manager.AddGoalArea(new CircleTemplate(1), new StaticTransform(goal, 0), [GoalWindow.Persistent(Start, 5, PositionPreference.Center)]);
        var snapshot = manager.BuildSnapshot(Start);
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, Start);
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "real temporal solver must preserve a route through sequential original-size AOE windows");
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(path.Count >= 2, "temporal route contains actual movement");
        foreach (var point in path)
            Check(e.Boundary.Contains(point.Position), "planned route remains inside the original arena edge");
    }

    private static void Lifecycle()
    {
        foreach (var e in Encounters())
        {
            Begin(e);
            e.StartCast(Cast(e.Actions[0], e.Center, 5));
            e.Cancel(10);
            e.Synchronize([], Start.AddSeconds(1));
            Check(!e.Active && e.Collect(Start.AddSeconds(1), 100).All(a => a.PreviewOnly), $"PT{e.Floor}: wipe clears active navigation state");
            Begin(e);
            Check(e.Active, $"PT{e.Floor}: a fresh pull can reactivate");
        }
    }
}
