using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class EnuoRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Enuo native identity arena map transitions retain pending fight", () =>
        {
            using var f = Scene();
            Check(f.Engine.Definitions.Single().BossOid == 0x4DB9 && ArrDungeonCatalog.IsScript("masterplayer.trial.enuo"), "Native identity.");
            Check(f.Engine.Arena!.Contains(new(119.9f, 0, 100)) && !f.Engine.Arena.Contains(new(120.1f, 0, 100)), "Initial R20.");
            Start(f, 10, 49963); f.Engine.MapEffect(0, 0x00020001); f.Render();
            Check(f.Engine.Arena.Contains(new(179.9f, 0, 179.9f)) && !f.Engine.Arena.Contains(new(180.1f, 0, 100))
                && f.Engine.OutputCasts.Any(), "Intermission square/retention.");
            f.Engine.MapEffect(0, 0x00080004); f.Render();
            Check(!f.Engine.Arena.Contains(new(121, 0, 100)), "Circle restore.");
        });
        run("Enuo cones share seven output cap and sort only at ten with source tie partition", () =>
        {
            using var f = Scene();
            for (var i = 0; i < 9; ++i) Start(f, (ulong)(10 + i), i % 2 == 0 ? 49952u : 49953u, duration: 20 - i);
            Check(Group(f, "cone").Length == 7 && Group(f, "cone")[0].Activation == f.Start.AddSeconds(20.3)
                && !f.Engine.Gazes.Any(), "Early order/cap or invented gaze.");
            Start(f, 19, 49953, duration: 1);
            Check(Group(f, "cone")[0].Activation == f.Start.AddSeconds(1.3) && Group(f, "cone").Length == 7, "Ten-caster sorting.");
            Hit(f, 19, 49953, 1); Check(Group(f, "cone").Length == 7, "Effect consumed simple cone.");
            End(f, 19, 49953); Check(Group(f, "cone")[0].Activation == f.Start.AddSeconds(12.3), "Finish consumption/order.");
        });
        run("Enuo silent torrent annular sectors preserve all three full angles and radii", () =>
        {
            using var f = Scene();
            foreach (var (action, degrees) in new[] { (49946u, 20f), (49947u, 40f), (49948u, 60f) })
            {
                Start(f, 10, action);
                Check(f.Engine.Casts[10].Rule.Shape is PtDonutSectorTemplate { Inner: 17, Outer: 19 } sector
                    && Math.Abs(sector.Angle - degrees * MathF.PI / 180) < .00001, "Sector shape.");
                End(f, 10, action); At(f, (f.Now - f.Start).TotalSeconds + 1);
            }
        });
        run("Enuo chase icon and first cast do not create a chaser tether does with six second world clock", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 172, f.Now, 102);
            Start(f, 10, 48474); Check(!Group(f, "chase").Any(), "Unused first action started chase.");
            Start(f, 11, 49940); Check(!Group(f, "chase").Any(), "Source override Start gained add behavior.");
            f.Engine.Tether(101, 404, true, f.Start.AddSeconds(-2), 10); f.Render();
            var display = Group(f, "chase").Single(a => a.Id.EndsWith("/display"));
            var avoid = Group(f, "chase").Single(a => !a.Display);
            Check(display.Shape is CircleTemplate { Radius: 6 } && display.Position == Quantized(f.Actors[10].Position)
                && avoid.Shape is CircleTemplate { Radius: 7 } && avoid.Activation == f.Start.AddSeconds(6), "Tether geometry/time.");
        });
        run("Enuo chase target navigation shifts radius and follows actual nearest hit not caster", () =>
        {
            using var f = Scene();
            f.Engine.Tether(100, 404, true, f.Now, 10); f.Engine.Tether(101, 404, true, f.Now, 11); f.Render();
            var avoid = Group(f, "chase").Single(a => a.Id.EndsWith("/avoid") && a.Shape is CircleTemplate { Radius: 8.9f });
            Check(avoid.Position == Quantized(f.Actors[10].Position) - Vector3.UnitZ * 6, "Own center offset.");
            At(f, 10);
            f.Engine.Effect(29, 49940, 1, f.Now, target: 29, occurredAt: f.Now.AddSeconds(-2)); f.Render();
            Check(Group(f, "chase").Any(a => !a.Display && a.Activation == f.Now.AddSeconds(.7)), "Closest chase not advanced on world clock.");
            var advanced = Group(f, "chase").Single(a => !a.Display && a.Activation == f.Now.AddSeconds(.7));
            Check(advanced.Position.X > 110, "Actual source location fallback not used.");
        });
        run("Enuo chase third tether clears before invalid target and fourth resets counter", () =>
        {
            using var f = Scene();
            f.Engine.Tether(100, 404, true, f.Now, 10); f.Engine.Tether(101, 404, true, f.Now, 11);
            f.Engine.Tether(999, 404, true, f.Now, 12); f.Render(); Check(!Group(f, "chase").Any(), "Invalid third target failed preclear.");
            f.Engine.Tether(102, 404, true, f.Now, 12); f.Engine.Tether(103, 404, true, f.Now, 13);
            f.Engine.Tether(104, 404, true, f.Now, 14); f.Render();
            Check(Group(f, "chase").Count(a => a.Id.EndsWith("/display")) == 3, "Fourth success did not reset cycle.");
            f.Engine.Tether(104, 404, false, f.Now, 14); f.Render();
            Check(Group(f, "chase").Count(a => a.Id.EndsWith("/display")) == 3, "Untether invented cancellation.");
        });
        run("Enuo chase consumes thirteen effects with no TTL and destroys all on chaser object removal", () =>
        {
            using var f = Scene(); f.Engine.Tether(100, 404, true, f.Now, 10);
            At(f, 100); Check(Group(f, "chase").Any(), "TTL removed source persistent chase.");
            for (uint i = 1; i <= 12; ++i) Hit(f, 10, 49940, i, 10);
            Check(Group(f, "chase").Any(), "Chase ended before thirteen.");
            Hit(f, 10, 49940, 13, 10); Check(!Group(f, "chase").Any(), "Thirteenth effect did not consume.");
            f.Engine.Tether(101, 404, true, f.Now, 11); f.Actors[30] = new(30, 0x4EB4, new(100, 0, 100)); f.Scan();
            f.Actors.Remove(30); f.Scan(); Check(!Group(f, "chase").Any(), "Chaser removal did not clear all.");
        });
        run("Enuo chase dead targets remain before first hit but clear after consumption", () =>
        {
            using var f = Scene(); f.Engine.Tether(101, 404, true, f.Now, 10);
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan(); Check(Group(f, "chase").Any(), "Premature dead target removal.");
            Hit(f, 10, 49940, 1, 10); f.Scan(); Check(!Group(f, "chase").Any(), "Consumed dead target not removed.");
        });
        run("Enuo deep freeze target circle persists through finish resolves by caster effect not icon", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 327, f.Now); f.Render(); Check(!Group(f, "freeze").Any(), "Icon started freeze.");
            Start(f, 10, 49966, 101); Start(f, 11, 49966, 102);
            Check(Group(f, "freeze").Count(a => a.Display) == 2 && f.Engine.PredictedDamage.Count() == 2
                && f.Engine.PredictedDamage.All(p => p.DamageType == "Raidwide"), "Freeze target prediction.");
            End(f, 10, 49966); At(f, 100); Check(Group(f, "freeze").Count(a => a.Display) == 2, "Finish/timeout consumed freeze.");
            f.Actors[101] = f.Actors[101] with { Position = new(90, 0, 100) }; f.Scan();
            Check(Group(f, "freeze").Any(a => a.Display && a.Position.X == 90), "Freeze did not follow target.");
            Hit(f, 10, 49966, 1, 102); Check(Group(f, "freeze").Count(a => a.Display) == 1, "Effect matched target rather than source.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan(); Check(!Group(f, "freeze").Any(), "Dead freeze target not cleared.");
        });
        run("Enuo holy stack and meltdown independent cast targets min four no shared prediction", () =>
        {
            using var f = Scene(); Start(f, 10, 49968, 100); Start(f, 11, 49964, 101);
            Check(Group(f, "stack").Single(a => a.Display).Shape is CircleTemplate { Radius: 7 }
                && Group(f, "spread").Single(a => a.Display).Shape is CircleTemplate { Radius: 5 }
                && Group(f, "spread").Any(a => !a.Display && a.Shape is CircleTemplate { Radius: 6 })
                && !f.Engine.PredictedDamage.Any(), "Stack/spread shape or invented prediction.");
            Hit(f, 10, 49968, 1, 100); Check(Group(f, "stack").Any(), "Stack consumed on effect.");
            End(f, 10, 49968); Check(!Group(f, "stack").Any() && Group(f, "spread").Any(), "Independent finish.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan(); Check(!Group(f, "spread").Any(), "Dead spread display.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan(); Check(Group(f, "spread").Any(), "Revival lost pending spread.");
        });
        run("Enuo icon line source target immediate timing actual action correction and no finish consumption", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 719, f.Start.AddSeconds(-3), 12); f.Render();
            var line = Group(f, "line").Single(a => a.Display);
            Check(line.Position == f.Actors[12].Position && line.Shape is RayRectangleTemplate { Length: 60, MinLocalX: -4, MaxLocalX: 4 }
                && Group(f, "line/join").Single().Activation == f.Start && f.Engine.PredictedDamage.Count() == 8, "Line origin/timing/prediction.");
            Start(f, 12, 49969, 101); End(f, 12, 49969); Hit(f, 12, 49970, 1, 101);
            Check(Group(f, "line").Any(), "Cast/wrong effect consumed line.");
            Hit(f, 12, 49969, 2, 102); Check(!Group(f, "line").Any(), "Single-target correction failed.");
            f.Engine.Icon(999, 719, f.Now, 12); f.Render(); Check(!Group(f, "line").Any(), "Missing target fallback invented.");
        });
        run("Enuo multiple icon lines require matching effect and allow dead targets", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 719, f.Now, 12); f.Engine.Icon(102, 719, f.Now, 13); f.Render();
            Hit(f, 12, 49969, 1, 103); Check(Group(f, "line").Count(a => a.Display) == 2, "Multi line fallback removed.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan(); Check(Group(f, "line").Count(a => a.Display) == 2, "Dead target filtered.");
            f.Actors[12] = f.Actors[12] with { Dead = true }; f.Scan(); Check(Group(f, "line").Count(a => a.Display) == 1, "Dead source not hidden.");
            Hit(f, 13, 49969, 2, 102); Check(!Group(f, "line").Any(), "Matching target resolution failed.");
        });
        run("Enuo LOS locks Zero shadows uses cast location permits hitbox and keeps snapshot after blocker motion", () =>
        {
            using var f = Scene(); f.Actors[30] = new(30, 0x4DBF, new(100, 0, 105), HitboxRadius: 2, Hostile: false, Dead: true); f.Scan();
            Start(f, 10, 49959, location: new(100, 0, 100));
            var los = Group(f, "los").Single(a => a.Display); var shape = los.Shape.Resolve(los.Position, 0, null);
            Check(shape.Contains(new(100, 0, 105.3f)) && !shape.Contains(new(100, 0, 105.1f))
                && !shape.Contains(new(105, 0, 110)), "LOS annular tangent geometry/impassable changed.");
            f.Actors[30] = f.Actors[30] with { Position = new(110, 0, 100) }; f.Scan();
            Check(Group(f, "los").Single(a => a.Display).Shape.Resolve(los.Position, 0, null).Contains(new(100, 0, 110)), "Moving blocker rebuilt snapshot.");
            Hit(f, 10, 49959, 1); Check(Group(f, "los").Any(), "LOS effect consumed.");
            End(f, 10, 49959); Check(!Group(f, "los").Any(), "LOS finish did not clear.");
        });
        run("Enuo LOS first only FIFO shared earliest caster origin and missing blocker emits nothing", () =>
        {
            using var f = Scene(); Start(f, 10, 49959); Check(!Group(f, "los").Any(), "No blocker invented full arena forbid."); End(f, 10, 49959);
            At(f, 1); f.Actors[30] = new(30, 0x4DBF, new(100, 0, 105), HitboxRadius: 2, Hostile: false); f.Scan();
            Start(f, 10, 49959, duration: 10); Start(f, 11, 49959, duration: 20, location: new(110, 0, 100));
            Check(Group(f, "los").Count(a => a.Display) == 1, "LOS cap.");
            End(f, 11, 49959);
            var los = Group(f, "los").Single(a => a.Display);
            Check(los.Activation == null && los.Position == new Vector3(100, 0, 100)
                && Group(f, "los").Single(a => !a.Display).Activation == f.Start.AddSeconds(21.3), "FIFO/shared earlier caster snapshot.");
        });
        run("Enuo source implemented shapes raidwides primary cleanup and personal death retention", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 49933u, 49963u, 49949u, 50667u, 49981u, 49958u, 49934u })
            {
                Start(f, 10, action); Check(f.Engine.OutputCasts.Single().Value.Rule.Shape != null && f.Draw.Previews.Count > 0, "Simple shape missing.");
                End(f, 10, action); At(f, (f.Now - f.Start).TotalSeconds + 1);
            }
            foreach (var action in new[] { 49971u, 49928u })
            {
                Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 8, "Raidwide missing."); End(f, 10, action);
            }
            Start(f, 11, 49963); f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(f.Engine.Active != null, "Personal death reset.");
            Check(!f.Engine.RequireAllStop && f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.Gazes.Any(), "Invented control.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Primary cleanup.");
        });
        run("Enuo two holy targets retain other target join constraint despite hostile display", () =>
        {
            using var f = Scene(); Start(f, 10, 49968, 100); Start(f, 11, 49968, 101);
            Check(Group(f, "stack").Any(a => a.Id.EndsWith("/buddy")) && Group(f, "stack/join").Any()
                && !Group(f, "stack").Any(a => a.Shape is CircleTemplate { Radius: 14 }), "Marked actor incorrectly blocked other stack.");
        });
        run("Enuo dead line sources keep their target excluded from active target buddy candidates", () =>
        {
            using var f = Scene();
            for (ulong i = 102; i <= 107; ++i) f.Actors[i] = f.Actors[i] with { Dead = true };
            f.Scan(); f.Engine.Icon(100, 719, f.Now, 12); f.Engine.Icon(101, 719, f.Now, 13);
            f.Actors[13] = f.Actors[13] with { Dead = true }; f.Scan();
            Check(Group(f, "line").Count(a => a.Display) == 1 && !Group(f, "line").Any(a => a.Id.EndsWith("/buddy")),
                "Dead source target became valid buddy.");
        });
        run("Enuo line position hints survive count TODO and satisfied holy does not demand extra soakers", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Position = new(80, 0, 110) }; f.Scan();
            f.Engine.Icon(101, 719, f.Now, 12);
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("进入直线分摊"), "Missing line join warning.");
            f.Engine.Icon(100, 719, f.Now, 13); f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 100) }; f.Scan();
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("离开其他直线分摊"), "Missing line overlap warning.");
            Hit(f, 12, 49969, 1, 101); Hit(f, 13, 49969, 2, 100);
            f.Actors[100] = f.Actors[100] with { Position = new(80, 0, 100) };
            for (ulong i = 101; i <= 104; ++i) f.Actors[i] = f.Actors[i] with { Position = new(100, 0, 100) };
            for (ulong i = 105; i <= 107; ++i) f.Actors[i] = f.Actors[i] with { Position = new(115, 0, 100) };
            f.Scan(); Start(f, 10, 49968, 101);
            Check(!f.Engine.Hints(f.Actors[100].Position).Contains("进入分摊"), "Satisfied holy stack demanded fifth participant.");
        });
    }
    private static Fight Scene()
    {
        var f = new Fight(0x4DB9, 1361); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i,
            PartySlot: i, PartyRole: i < 2 ? "Tank" : "Melee", Hostile: false);
        for (ulong i = 10; i < 30; ++i) f.Actors[i] = new(i, 0x233C, c + Vector3.UnitX * ((int)i - 10));
        f.Scan(); return f;
    }
    private static Vector3 Quantized(Vector3 p)
    {
        var q = new BossMod.WPos(p.X, p.Z).Quantized(); return new(q.X, p.Y, q.Z);
    }
    private static void At(Fight f, double seconds) { f.Now = f.Start.AddSeconds(seconds); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0, Vector3? location = null, double duration = 5)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, 0, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static void Hit(Fight f, ulong source, uint action, uint sequence, ulong target = 0)
    { f.Engine.Effect(source, action, sequence, f.Now, target); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("enuo/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
