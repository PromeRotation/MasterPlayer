using System.Numerics;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class Program
{
    internal static readonly DateTime Epoch = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    private static Vector3 Center => PalaceArena.Center;

    private static int Main()
    {
        (string Name, Action Body)[] cases =
        [
            ("actual five ARR recording timelines", RecordingRegression.Run),
            ("real solver detours around native R9 pool", SolverDetour),
            ("160 exact Infatuation and delayed Hall; deduplication", InfatuationAndHall),
            ("160 dynamic charge; personal knockback and immunities", Valfodr),
            ("160 170 190 cleave perspective and ally avoidance", Cleaves),
            ("170 Douse source; locked Electrogenesis; dynamic pull goal", DouseAndPull),
            ("180 locked fans; tornado; strict target-only HP hold", Behemoth),
            ("190 birth timer; source cleanup; gray bomb prohibition", Bombs),
            ("raidwide hints retain safe space", Raidwides),
            ("persistent objects; state7; prediction handoff; reset", ObjectsAndReset),
            ("all gazes and interrupts; ignored Chirp", FloorCasts),
            ("spikes ten seconds; refresh; removal; target priority", Spikes),
            ("old-action cancel and interrupt preserve replacement", Cancellation),
        ];
        var failures = 0;
        foreach (var (name, body) in cases)
        {
            try { body(); Console.WriteLine("PASS " + name); }
            catch (Exception ex) { ++failures; Console.WriteLine("FAIL " + name + ": " + ex); }
        }
        Console.WriteLine($"{cases.Length - failures} passed; {failures} failed.");
        return failures == 0 ? 0 : 1;
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static ReplayActorState Actor(ulong id, uint oid, Vector3 position, ulong target = 0) => new()
    {
        Id = id, Oid = oid, Position = position, TargetId = target, InCombat = true,
        Hostile = true, Targetable = true, Hp = 100000, MaxHp = 100000,
        HitboxRadius = oid == 0x181E ? 5.75f : 3.92f,
    };
    private static ReplayActorState Player(ulong id, Vector3 position)
        => Actor(id, 0, position) with { Kind = 1, Hostile = false, HitboxRadius = .5f };
    private static PalaceEncounter Scene(uint oid = 0x181D)
    {
        var encounter = new PalaceEncounter();
        encounter.Synchronize([Actor(10, oid, Center, 100), Player(100, Center + new Vector3(0, 0, 10)),
            Player(101, Center + new Vector3(10, 0, 0))], Epoch);
        return encounter;
    }
    private static PalaceCast Cast(uint action, ulong source = 10, ulong target = 100, Vector3? position = null,
        float? rotation = 0, GeometryTemplate? shape = null)
        => new(source, action, target, position ?? Center, Center, rotation, Epoch, Epoch.AddSeconds(4), shape);
    private static LogSystemActionEffectEvent Effect(uint action, ulong source = 10, uint sequence = 42, Vector3? target = null)
        => new() { SourceId = source, ActionId = action, GlobalSequence = sequence,
            SourcePosition = Center, TargetPosition = target ?? Center };

    private static void SolverDetour()
    {
        var e = new PalaceEncounter();
        var start = Center + new Vector3(-16, 0, 0);
        var goal = Center + new Vector3(16, 0, 0);
        e.Synchronize([Actor(20, 0x1E858E, Center)], Epoch);
        var area = e.Collect(Epoch, 100).Single();
        var snapshot = new SafeZoneSnapshot(1, Epoch, PalaceArena.Boundary, [],
            [new DangerAreaSnapshot(default, area.Geometry, [area.Window])],
            [new GoalAreaSnapshot(default, new CircleGeometry(goal, .5f), [GoalWindow.Persistent(Epoch, 5, PositionPreference.Center)])]);
        var solution = new SafeZoneSolver().Solve(snapshot,
            new(start, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6),
            new SolveOptions { GridResolution = .25f, HardSafetyMargin = .5f, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, Epoch);
        Check(solution.IsSuccess && !solution.IsLeastDangerFallback && !solution.IsNoMovement, "Strict moving success required: " + solution.Status);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(path.Count >= 3 && Vector3.Distance(path[^1].Position, goal) < 1, "Expected bent route to goal");
        var samples = 0;
        var length = 0f;
        var minRadius = float.MaxValue;
        for (var i = 1; i < path.Count; ++i)
        {
            var a = path[i - 1].Position;
            var b = path[i].Position;
            length += Vector3.Distance(a, b);
            var count = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(a, b) / .05f));
            for (var j = 0; j <= count; ++j)
            {
                var point = Vector3.Lerp(a, b, (float)j / count);
                ++samples;
                Check(PalaceArena.Boundary.Contains(point) && !area.Geometry.Contains(point), "Route crossed pool or arena wall");
                minRadius = MathF.Min(minRadius, Vector3.Distance(point, Center));
            }
        }
        Check(length > 33, "Route cut straight through pool");
        Check(!PalaceArena.Boundary.Contains(Center + new Vector3(0, 0, -24)), "Entrance cut was omitted");
        Console.WriteLine($"  solver={solution.Status}; points={path.Count}; samples={samples}; length={length:F3}; minRadius={minRadius:F3}");
    }

    private static void InfatuationAndHall()
    {
        var e = Scene();
        e.StartCast(Cast(7090));
        Check(e.Collect(Epoch, 100).Single(a => a.Action == 7090).Shape is CircleTemplate { Radius: 7 }, "Infatuation radius");
        e.Cancel(10);
        var effect = Effect(7155, target: Center + new Vector3(4, 0, 2));
        e.Apply(effect, Epoch);
        e.Apply(effect with { TargetIndex = 1, TargetId = 101 }, Epoch);
        var area = e.Collect(Epoch, 100).Single(a => a.Action == 7155);
        Check(area.Shape is CircleTemplate { Radius: 9 } && area.From == Epoch.AddSeconds(1.3)
            && area.Position == effect.TargetPosition, "Hall origin/timing/radius");
    }

    private static void Valfodr()
    {
        var e = Scene();
        e.StartCast(Cast(7156));
        var area = e.Collect(Epoch, 101).Single(a => a.Action == 7156);
        Check(area.Shape is RayRectangleTemplate { Length: 10 } r && r.MaxLocalX - r.MinLocalX == 6, "Charge width/length");
        Check(e.Collect(Epoch, 100).Any(a => a.Action == 7156 && a.PreviewOnly && a.OutlineOnly), "Own charge outline");
        Check(!e.Collect(Epoch, 100).Any(a => a.Action == 7156 && !a.NavigationOnly && !a.PreviewOnly), "Own charge damage rectangle");
        Check(e.Collect(Epoch, 100).Any(a => a.Key == "landing/10"), "Missing landing constraint");
        e.Synchronize([Actor(10, 0x181D, Center, 100), Player(100, Center + new Vector3(10, 0, 0)), Player(101, Center)], Epoch.AddSeconds(1));
        Check(MathF.Abs(e.Collect(Epoch.AddSeconds(1), 101).Single(a => a.Action == 7156).Rotation - MathF.PI / 2) < .001f, "Charge did not track target");
        foreach (uint sid in new uint[] { 160, 1209, 1096, 1512 })
            foreach (var expiry in new[] { 3, 5 })
            {
                e.Synchronize([Actor(10, 0x181D, Center, 100), Player(100, Center + new Vector3(0, 0, 10)) with
                    { Statuses = [new(sid, 0, 100, Epoch.AddSeconds(expiry))] }], Epoch.AddSeconds(1));
                Check(e.Collect(Epoch.AddSeconds(1), 100).Any(a => a.Key == "landing/10") == (expiry == 3), "Immunity expiration");
            }
        var template = new PalaceLandingTemplate(Center + new Vector3(-10, 0, 0), []);
        var point = Center + new Vector3(-9, 0, 0);
        Check(MathF.Abs(Vector3.Distance(point, template.Landing(point)) - 25) < .001f, "Knockback distance");
        template = new(Center, []);
        point = Center + new Vector3(0, 0, 10);
        var end = template.Landing(point);
        Check(PalaceArena.Boundary.Contains(end) && end.Z > Center.Z + 24, "Wall clipping");
        Check(new PalaceLandingTemplate(Center, [new CircleGeometry(end, 1)]).Resolve(Center, 0, null).Contains(point), "Unsafe landing accepted");
        Check(!template.Resolve(Center, 0, null).Contains(point), "Safe wall landing forbidden");
    }

    private static void Cleaves()
    {
        foreach (uint oid in new uint[] { 0x181D, 0x181E, 0x1820 })
        {
            var e = Scene(oid);
            Check(e.Collect(Epoch, 100).Any(a => a.Key == "cleave/10" && a.PreviewOnly), "Tank preview");
            Check(e.Collect(Epoch, 100).Single(a => a.Key == "cleave-bait/10/101").NavigationOnly, "Ally sector");
            Check(e.Collect(Epoch, 101).Any(a => a.Key == "cleave/10" && !a.PreviewOnly), "Perspective");
            e.StartCast(Cast(7161));
            Check(!e.Collect(Epoch, 100).Any(a => a.Key.StartsWith("cleave")), "Cleave active during cast");
        }
    }

    private static void DouseAndPull()
    {
        var e = Scene(0x181E);
        e.Apply(Effect(7158, target: Center + new Vector3(10, 0, 0)), Epoch);
        var area = e.Collect(Epoch, 100).Single(a => a.Action == 7158);
        Check(area.Position == Center && area.From == Epoch.AddSeconds(.8) && area.Shape is CircleTemplate { Radius: 8 }, "Douse source/timing");
        var loc = Center + new Vector3(2, 0, 3);
        e.StartCast(Cast(7161, position: loc));
        Check(e.Collect(Epoch, 100).Single(a => a.Action == 7161).Position == loc, "Electrogenesis lock");
        var actors = new[] { Actor(10, 0x181E, Center, 100), Player(100, Center + new Vector3(0, 0, -15)), Player(101, Center), Actor(20, 0x1E9998, Center) };
        e.Synchronize(actors, Epoch);
        var goal = e.Collect(Epoch, 100).Single(a => a.Key == "pull/170");
        Check(goal.Safe && goal.NavigationOnly && !goal.Geometry.Contains(Center)
            && goal.Geometry.Contains(Center + new Vector3(0, 0, 24)), "Pull goal region");
        Check(!e.Collect(Epoch, 101).Any(a => a.Key == "pull/170"), "Non-tank pull");
        e.Synchronize(actors.Select(a => a.Id == 20 ? a with { EventState = 7 } : a), Epoch.AddSeconds(1));
        Check(!e.Collect(Epoch.AddSeconds(1), 100).Any(a => a.Key == "pull/170"), "State7 pull retained");
    }

    private static void Behemoth()
    {
        var e = Scene(0x181F);
        foreach (var (action, radius, angle) in new[] { (7162u, 16.6f, MathF.PI * 2 / 3), (7165u, 51.6f, MathF.PI / 3) })
        {
            var loc = Center + new Vector3(3, 0, 2);
            e.StartCast(Cast(action, position: loc, rotation: MathF.PI / 2));
            e.Synchronize([Actor(10, 0x181F, Center + new Vector3(8, 0, 9)) with { Rotation = 2 }, Player(100, Center)], Epoch.AddSeconds(1));
            var area = e.Collect(Epoch.AddSeconds(1), 100).Single(a => a.Action == action);
            Check(area.Position == loc && area.Rotation == MathF.PI / 2 && area.Shape is FanTemplate f
                && f.Radius == radius && f.AngleRadians == angle, "Fan lock or exact size");
            e.StartCast(Cast(action, rotation: null));
            Check(!e.Collect(Epoch, 100).Any(a => a.Action == action), "Guessed missing rotation");
        }
        e.StartCast(Cast(7163));
        Check(e.Collect(Epoch, 100).Single(a => a.Action == 7163).Shape is CircleTemplate { Radius: 6 }, "Charybdis");
        e.Synchronize([Actor(10, 0x181F, Center), Player(100, Center), Actor(20, 0x18F0, Center)], Epoch);
        Check(e.Collect(Epoch, 100).Single(a => a.Owner == 20).Shape is CircleTemplate { Radius: 10 }, "Tornado");
        foreach (uint hp in new uint[] { 14999, 15000, 15001, 15799, 15800, 15801 })
        {
            e.Synchronize([Actor(10, 0x181F, Center) with { Hp = hp }], Epoch);
            Check(e.ForbiddenTargets(Epoch).Contains(10ul) == (hp > 15000 && hp < 15800), "Strict HP hold " + hp);
            Check(e.ForbiddenTargets(Epoch).All(id => id == 10), "Hold prohibited other targets");
        }
        e = Scene(0x181F);
        uint[] sequence = [7162, 7163, 7163, 7162, 7165, 7163, 7163, 7162, 7163, 7165];
        for (var i = 0; i < sequence.Length; ++i)
        {
            e.Apply(Effect(sequence[i], sequence: (uint)i + 1), Epoch);
            var next = (i + 1) % 10 + 1;
            Check(e.Hints(Epoch).Any(h => h.Contains($"循环 {next}/10")), "Rotation prediction did not advance");
        }
        e.Apply(Effect(7166, sequence: 100), Epoch);
        Check(!e.Hints(Epoch).Any(h => h.Contains("循环")), "Meteor retained rotation prediction");
    }

    private static void Bombs()
    {
        var e = Scene(0x1820);
        var actors = new[] { Actor(10, 0x1820, Center), Player(100, Center), Actor(20, 0x18F3, Center), Actor(21, 0x18F3, Center),
            Actor(22, 0x18F2, Center + new Vector3(7.1f, 0, 0)), Actor(23, 0x18F2, Center + new Vector3(7.3f, 0, 0)) };
        e.Synchronize(actors, Epoch);
        var bomb = e.Collect(Epoch, 100).Single(a => a.Owner == 20);
        Check(bomb.From == Epoch.AddSeconds(9.8) && bomb.Shape is CircleTemplate { Radius: 6.6f }, "Birth timer/radius");
        e.Synchronize(actors, Epoch.AddSeconds(5));
        Check(e.Collect(Epoch.AddSeconds(5), 100).Single(a => a.Owner == 20).From == bomb.From, "Timer reset");
        e.Apply(Effect(7172, source: 20), Epoch.AddSeconds(10));
        Check(!e.Collect(Epoch.AddSeconds(10), 100).Any(a => a.Owner is 20 or 21), "Source small-bomb wave cleanup");
        Check(e.ForbiddenTargets(Epoch).Contains(22ul) && !e.ForbiddenTargets(Epoch).Contains(23ul), "Gray distance");
        e.StartCast(Cast(7170, source: 22));
        Check(e.Collect(Epoch, 100).Single(a => a.Action == 7170).Shape is CircleTemplate { Radius: 7.2f }, "Flashthoom");
        e.StartCast(Cast(7169));
        Check(e.Collect(Epoch, 100).Single(a => a.Action == 7169).Shape is CircleTemplate { Radius: 8 }, "Sap");
    }

    private static void Raidwides()
    {
        foreach (var (action, oid) in new[] { (7166u, 0x181Fu), (7103u, 0x1820u), (7171u, 0x1820u) })
        {
            var e = Scene(oid);
            e.StartCast(Cast(action, shape: new CircleTemplate(50)));
            Check(!e.Collect(Epoch, 100).Any(a => a.Action == action) && e.HasResponse(action, Epoch) && e.Hints(Epoch).Any(), "Raidwide geometry/hint");
        }
    }

    private static void ObjectsAndReset()
    {
        foreach (var (oid, radius) in new[] { (0x1E858Eu, 9f), (0x1E9998u, 8f), (0x18F0u, 10f) })
        {
            var e = new PalaceEncounter();
            e.Synchronize([Actor(20, oid, Center)], Epoch);
            Check(e.Collect(Epoch, 100).Single().Shape is CircleTemplate c && c.Radius == radius, "Object radius");
            foreach (var a in new[] { Actor(20, oid, Center) with { EventState = 7 }, Actor(20, oid, Center) with { Dead = true } })
            {
                e.Synchronize([a], Epoch);
                Check(e.Collect(Epoch, 100).Count == 0, "Dead/state7 object");
            }
            e.Synchronize([], Epoch);
            Check(e.Collect(Epoch, 100).Count == 0, "Removed object");
        }
        foreach (var (action, oid) in new[] { (7155u, 0x1E858Eu), (7158u, 0x1E9998u) })
        {
            var e = Scene();
            var effect = Effect(action);
            e.Apply(effect, Epoch);
            e.Synchronize([Actor(20, oid, Center)], Epoch.AddSeconds(2));
            Check(e.Collect(Epoch.AddSeconds(2), 100).Count == 1, "Prediction duplicated");
            e.Synchronize([Actor(20, oid, Center) with { EventState = 7 }], Epoch.AddSeconds(3));
            Check(e.Collect(Epoch.AddSeconds(3), 100).Count == 0, "Prediction resurrected");
            e.Reset();
            Check(!e.BossActive && e.Actors.Count == 0 && e.Collect(Epoch, 100).Count == 0, "Reset retained state");
            e.Apply(effect, Epoch);
            Check(e.Collect(Epoch, 100).Count == 1, "Reset retained effect identity");
        }
    }

    private static void FloorCasts()
    {
        var e = new PalaceEncounter();
        e.Synchronize([Actor(10, 0x1890, Center), Player(100, Center + new Vector3(0, 0, 5)), Player(101, Center + new Vector3(0, 0, -5))], Epoch);
        e.StartCast(Cast(6356));
        Check(e.Facing(Epoch.AddSeconds(3.5), 100) != null && e.Facing(Epoch.AddSeconds(3.5), 101) == null, "Cone gaze");
        e.StartCast(Cast(6356, rotation: null));
        Check(e.Facing(Epoch.AddSeconds(3.5), 100) == null, "Guessed gaze direction");
        foreach (var (action, inside, outside) in new[] { (6351u, 99f, 101f), (6953u, 29f, 31f) })
        {
            e.Synchronize([Actor(10, 0x1890, Center), Player(100, Center + new Vector3(0, 0, inside)), Player(101, Center + new Vector3(0, 0, outside))], Epoch);
            e.StartCast(Cast(action, target: 101));
            Check(e.Facing(Epoch.AddSeconds(3.5), 100) != null && e.Facing(Epoch.AddSeconds(3.5), 101) == null, "Gaze range/perspective");
            Check(e.Facing(Epoch.AddSeconds(3), 100) == null, "Facing too early");
            Check(!e.Collect(Epoch, 100).Any(a => a.Action == action), "Gaze became circle");
        }
        foreach (var action in PalaceCatalog.Interrupts)
        {
            e.StartCast(Cast(action));
            Check(e.HasResponse(action, Epoch) && e.Hints(Epoch).Any(h => h.Contains("打断")), "Interrupt " + action);
            e.Apply(new LogSystemInterruptCastEvent { SourceId = 10, ActionId = action }, Epoch);
            Check(!e.HasResponse(action, Epoch), "Interrupt retained cast");
        }
        e.StartCast(Cast(6365, shape: new CircleTemplate(21.6f)));
        Check(!e.Collect(Epoch, 100).Any(a => a.Action == 6365), "Ignored Chirp");
    }

    private static void Spikes()
    {
        foreach (uint sid in new uint[] { 197, 198 })
        {
            var e = new PalaceEncounter();
            var mob = Actor(10, 0x1842, Center, 100) with { Statuses = [new(sid, 0, 10, Epoch.AddSeconds(60))] };
            e.Synchronize([mob], Epoch);
            Check(e.ForbiddenAutoAttackTargets(Epoch.AddSeconds(9.9)).Contains(10ul) && !e.ForbiddenTargets(Epoch).Contains(10ul) && e.PreferredTarget(100, Epoch) == 10, "Spikes absent");
            foreach (var seconds in new[] { 10.1, 10.2, 20.3, 30.4 })
            {
                var now = Epoch.AddSeconds(seconds);
                e.Synchronize([mob], now);
                Check(!e.ForbiddenAutoAttackTargets(now).Contains(10ul), "Snapshot rearmed spikes");
            }
            Check(e.PreferredTarget(100, Epoch.AddSeconds(31)) == 10 && e.PreferredTarget(101, Epoch.AddSeconds(31)) == 0, "Priority perspective");
            e.Apply(new LogSystemStatusAddEvent { TargetId = 10, StatusId = sid }, Epoch.AddSeconds(31));
            Check(e.ForbiddenAutoAttackTargets(Epoch.AddSeconds(31.1)).Contains(10ul), "Refresh ignored");
            e.Apply(new LogSystemStatusRemoveEvent { TargetId = 10, StatusId = sid }, Epoch.AddSeconds(32));
            Check(!e.ForbiddenAutoAttackTargets(Epoch.AddSeconds(32)).Contains(10ul), "Removal ignored");
        }
    }

    private static void Cancellation()
    {
        foreach (var interrupt in new[] { false, true })
        {
            var e = Scene();
            e.StartCast(Cast(7161));
            e.StartCast(Cast(7169));
            LogSystemEvent Cancel(uint action) => interrupt ? new LogSystemInterruptCastEvent { SourceId = 10, ActionId = action }
                : new LogSystemCancelCastEvent { SourceId = 10, ActionId = action };
            e.Apply(Cancel(7161), Epoch.AddSeconds(1));
            Check(e.HasResponse(7169, Epoch.AddSeconds(1)), "Old action deleted new cast");
            e.Apply(Cancel(7169), Epoch.AddSeconds(1));
            Check(!e.HasResponse(7169, Epoch.AddSeconds(1)), "Matching cancel ignored");
        }
        var encounter = Scene();
        encounter.StartCast(Cast(7156));
        encounter.Apply(new LogSystemCancelCastEvent { SourceId = 10, ActionId = 540, Raw = new { Param2 = 1u, Param3 = 7156u } }, Epoch);
        Check(!encounter.HasResponse(7156, Epoch), "Build14251465 raw action not normalized");
        encounter.StartCast(Cast(7156));
        encounter.Apply(new LogSystemCancelCastEvent { SourceId = 10, ActionId = 540,
            Raw = System.Text.Json.JsonSerializer.SerializeToElement(new { Param2 = 1u, Param3 = 7156u }) }, Epoch);
        Check(!encounter.HasResponse(7156, Epoch), "Recorded raw JSON action not normalized");
    }
}
