using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class ArkveldRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Arkveld native identity actual WIP component set and arena", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.arkveld") && f.Engine.Arena!.Contains(new(119.9f, 0, 100))
                && !f.Engine.Arena.Contains(new(120.1f, 0, 100)), "Identity/arena.");
            foreach (var action in new[] { 43835u, 43839u, 43840u, 45070u, 44807u, 44808u })
            {
                Start(f, 10, action); Check(!f.Engine.OutputCasts.Any() && !Group(f, "rush").Any(), "Unimplemented source enum gained mechanism.");
                End(f, 10, action);
            }
        });
        run("Arkveld chainblade all eight lines and radiance four cleaves preserve all simultaneous casts", () =>
        {
            using var f = Scene();
            uint[] lines = [43830, 43831, 45050, 45051, 45054, 45055, 45057, 45058];
            for (var i = 0; i < lines.Length; ++i) Start(f, (ulong)(10 + i), lines[i]);
            Check(f.Engine.OutputCasts.Count() == 8 && f.Engine.OutputCasts.All(c => c.Value.Rule.Shape is RayRectangleTemplate r
                && r.Length == 40 && r.MaxLocalX - r.MinLocalX == 4), "Line width/count.");
            uint[] cleaves = [43832, 43833, 45056, 45059];
            for (var i = 0; i < cleaves.Length; ++i) Start(f, (ulong)(20 + i), cleaves[i]);
            Check(f.Engine.OutputCasts.Count() == 12 && f.Engine.Casts[20].Rule.Shape is RayRectangleTemplate r
                && r.Length == 80 && r.MaxLocalX - r.MinLocalX == 28, "Cleave geometry/cap.");
        });
        run("Arkveld concentric rings remain cast driven and do not advance from time or effects", () =>
        {
            using var f = Scene();
            foreach (var (action, inner, outer) in new[] { (43844u, 0f, 8f), (43845u, 8f, 14f), (43846u, 14f, 20f), (43847u, 20f, 26f) })
            {
                Start(f, 10, action);
                Check(inner == 0 ? f.Engine.Casts[10].Rule.Shape is CircleTemplate { Radius: 8 }
                    : f.Engine.Casts[10].Rule.Shape is DonutTemplate d && d.InnerRadius == inner && d.OuterRadius == outer, "Ring parameters.");
                Hit(f, 10, action, action); At(f, (f.Now - f.Start).TotalSeconds + 50);
                Check(f.Engine.OutputCasts.Any(), "Effect/time consumed simple cast."); End(f, 10, action);
            }
        });
        run("Arkveld rush uses source snapshot quantization and unquantized vector width twelve", () =>
        {
            using var f = Scene(); f.Actors[10] = f.Actors[10] with { Position = new(95.004f, 0, 100.003f) }; f.Scan();
            Start(f, 10, 43842, location: new(110.006f, 0, 105.002f), rotation: 2);
            var rush = Group(f, "rush").Single();
            var source = new BossMod.WPos(95.004f, 100.003f).Quantized();
            Check(rush.Position == new Vector3(source.X, 0, source.Z) && rush.Shape is RayRectangleTemplate r
                && r.MaxLocalX - r.MinLocalX == 12 && Math.Abs(r.Length - new Vector2(15.002f, 4.999f).Length()) < .00002
                && Math.Abs(rush.Rotation - MathF.Atan2(15.002f, 4.999f)) < .00001, "Charge used packet facing or quantized length.");
            End(f, 10, 43842); Check(!Group(f, "rush").Any(), "Charge finish.");
        });
        run("Arkveld vengeance two caster matched actual hits lock time and yellow first circle no timeout", () =>
        {
            using var f = Scene(); Start(f, 10, 43860);
            var at = Group(f, "vengeance").Single().Activation;
            Check(Group(f, "vengeance").Single() is { Shape: CircleTemplate { Radius: 6 }, Color: var color }
                && color == new Vector4(1, 1, 0, 1) && at == f.Start.AddSeconds(5.3), "Initial exaflare.");
            End(f, 10, 43860); At(f, 50); Check(Group(f, "vengeance").Length == 1, "Cast/time consumed exaflare.");
            Hit(f, 11, 43861, 1, location: new(110, 0, 110)); Check(Group(f, "vengeance").Single().Activation == at, "Untracked caster advanced.");
            Hit(f, 10, 43861, 2, location: new(110.003f, 0, 110.006f));
            var position = new BossMod.WPos(110.003f, 110.006f).Quantized();
            Check(Group(f, "vengeance").Single().Position == new Vector3(position.X, 0, position.Z)
                && Group(f, "vengeance").Single().Activation == f.Now.AddSeconds(1.6), "Actual target location/next time.");
            Hit(f, 10, 43861, 3, location: new(115, 0, 115)); Check(!Group(f, "vengeance").Any(), "Second matching hit failed clear.");
        });
        run("Arkveld no cast pulses snapshot source expire exactly after float point eight", () =>
        {
            using var f = Scene(); f.Actors[10] = f.Actors[10] with { Rotation = .7f }; f.Scan();
            Hit(f, 10, 43875, 1);
            var pulse = Group(f, "pulse").Single();
            Check(pulse.Position == f.Actors[10].Position && pulse.Rotation == .7f && pulse.Activation == f.Start.AddSeconds((double).8f), "Pulse snapshot/time.");
            f.Now = pulse.Activation!.Value.AddTicks(-1); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "pulse").Any(), "Pulse expired before activation.");
            f.Now = pulse.Activation!.Value; f.Engine.Tick(f.Now); f.Render(); Check(!Group(f, "pulse").Any(), "Pulse exact expiry.");
        });
        run("Arkveld asymmetrical primary sweep lane preserves double rear coverage and expires at finish", () =>
        {
            using var f = Scene(); Start(f, 10, 45047); Check(!Group(f, "lane").Any(), "Helper created primary-only lane.");
            Start(f, 1, 45047); var lane = Group(f, "lane").Single();
            var shape = lane.Shape.Resolve(lane.Position, lane.Rotation, null);
            Check(shape.Contains(new(159, 0, 60.1f)) && shape.Contains(new(99.1f, 0, 139.9f))
                && !shape.Contains(new(98.9f, 0, 100)) && !shape.Contains(new(100, 0, 140.1f)), "Asymmetric shifted rectangle.");
            At(f, 5.3); Check(!Group(f, "lane").Any(), "Lane remained after activation despite no finish.");
            At(f, 6); Start(f, 1, 45046); lane = Group(f, "lane").Single(); shape = lane.Shape.Resolve(lane.Position, 0, null);
            Check(shape.Contains(new(40.1f, 0, 100)) && !shape.Contains(new(101.1f, 0, 100)), "Left sweep mirror.");
            End(f, 1, 45046); Check(!Group(f, "lane").Any(), "Lane cancellation.");
        });
        run("Arkveld stack icon actor eight point four delay exact resolving action target fallback", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 100, f.Now, actorId: 102); f.Render();
            var expected = new BossMod.WPos(102, 100).Quantized();
            Check(Group(f, "stack").Single(a => a.Display).Position == new Vector3(expected.X, 0, expected.Z)
                && Group(f, "stack/join").Single().Activation == f.Start.AddSeconds(8.4) && !f.Engine.PredictedDamage.Any(), "Stack actor/time or invented prediction.");
            Hit(f, 1, 43884, 1, 102); Hit(f, 1, 44811, 2, 102); Check(Group(f, "stack").Any(), "Enum comments replaced actual action.");
            Hit(f, 1, 43883, 3, 999); Check(!Group(f, "stack").Any(), "Actual action fallback clear.");
        });
        run("Arkveld stack empty effect advances source cast counter and prevents later equality clear", () =>
        {
            using var f = Scene(); Hit(f, 1, 43883, 1); f.Engine.Icon(101, 100, f.Now); f.Render();
            Hit(f, 1, 43883, 2, 101); Check(Group(f, "stack").Any(), "Source empty-hit counter behavior changed.");
        });
        run("Arkveld wild energy six display seven navigation death hide revival and accepted target finish", () =>
        {
            using var f = Scene(); Start(f, 10, 43866, target: 101);
            Check(Group(f, "spread").Single(a => a.Display).Shape is CircleTemplate { Radius: 6 }
                && Group(f, "spread").Single(a => !a.Display).Shape is CircleTemplate { Radius: 7 }
                && !f.Engine.PredictedDamage.Any(), "Spread dimensions/prediction.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan(); Check(!Group(f, "spread").Any(), "Dead spread visible.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan(); Check(Group(f, "spread").Any(), "Dead spread was deleted.");
            f.Engine.Finish(10, 43866, f.Start.AddSeconds(-1)); f.Render(); Check(Group(f, "spread").Any(), "Stale finish consumed.");
            End(f, 10, 43866); Check(!Group(f, "spread").Any(), "Finish target consumption.");
        });
        run("Arkveld towers separate small and tank large predictions and non tank hard forbidden", () =>
        {
            using var f = Scene(); Start(f, 10, 43853); Start(f, 11, 43854);
            Check(f.Engine.PredictedDamage.Any(p => p.DamageType == "Raidwide") && f.Engine.PredictedDamage.Count(p => p.DamageType == "Tankbuster") == 1, "Tower predicted masks.");
            f.Actors[100] = f.Actors[100] with { PartyRole = "Melee" }; f.Scan();
            Check(Group(f, "tower").Any(a => a.Id.EndsWith("/nontank")) && !f.Engine.PredictedDamage.Any(p => p.DamageType == "Tankbuster"), "Large tower role restriction.");
            End(f, 10, 43853); End(f, 11, 43854); Check(!Group(f, "tower").Any(), "Tower finish cleanup.");
        });
        run("Arkveld raidwides source actions only ordinary geometry no controls primary cleanup", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 43886u, 45201u, 43870u, 44790u, 44791u })
            {
                Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 8, "Raidwide action."); End(f, 10, action);
            }
            Check(!f.Engine.RequireAllStop && !f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Invented controls.");
            Start(f, 11, 43860); f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(Group(f, "vengeance").Any(), "Personal death erased phase.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Primary cleanup.");
        });
        run("Arkveld missing helper consumes just one of simultaneous spread casts on the same target", () =>
        {
            using var f = Scene(); Start(f, 10, 43866, target: 101); Start(f, 11, 43866, target: 101);
            f.Actors.Remove(10); f.Scan();
            Check(Group(f, "spread").Count(a => a.Display) == 1 && f.Engine.Casts.ContainsKey(11), "Missing helper consumed another cast.");
            End(f, 11, 43866); Check(!Group(f, "spread").Any(), "Remaining helper failed clear.");
        });
        run("Arkveld delayed effects use processing world clock for exaflare and pulse", () =>
        {
            using var f = Scene(); Start(f, 10, 43860); At(f, 10);
            f.Engine.Effect(10, 43861, 1, f.Now, location: new(110, 0, 110), occurredAt: f.Now.AddSeconds(-2));
            f.Engine.Effect(11, 43875, 2, f.Now, occurredAt: f.Now.AddSeconds(-2)); f.Render();
            Check(Group(f, "vengeance").Single().Activation == f.Now.AddSeconds(1.6)
                && Group(f, "pulse").Single().Activation == f.Now.AddSeconds((double).8f), "Packet time shortened world-clock prediction.");
        });
        run("Arkveld correctly soaked tower suppresses other empty towers in the same component", () =>
        {
            using var f = Scene();
            foreach (var id in f.Actors.Keys.Where(id => id is >= 101 and <= 107).ToArray())
                f.Actors[id] = f.Actors[id] with { Position = new(110, 0, 110) };
            f.Scan(); Start(f, 10, 43853); Start(f, 11, 43853, location: new(90, 0, 100));
            Check(!f.Engine.Hints(f.Actors[100].Position).Contains("每塔一人"), "Empty remote tower warned a correct soaker.");
            End(f, 10, 43853); End(f, 11, 43853); At(f, 1);
            Start(f, 10, 43854); Start(f, 11, 43854, location: new(90, 0, 100));
            Check(!f.Engine.Hints(f.Actors[100].Position).Contains("每塔一人"), "Tank tower component used per-tower hints.");
        });
    }
    private static Fight Scene()
    {
        var f = new Fight(0x48E2, 1300); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i,
            PartySlot: i, PartyRole: i == 0 ? "Tank" : "Melee", Hostile: false);
        for (ulong i = 10; i < 30; ++i) f.Actors[i] = new(i, 0x233C, c);
        f.Scan(); return f;
    }
    private static void At(Fight f, double seconds) { f.Now = f.Start.AddSeconds(seconds); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0, Vector3? location = null, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static void Hit(Fight f, ulong source, uint action, uint seq, ulong target = 0, Vector3? location = null)
    { f.Engine.Effect(source, action, seq, f.Now, target, location); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("ark/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
