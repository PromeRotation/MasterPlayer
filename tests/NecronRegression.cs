using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class NecronRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Necron native identity exact rectangular split and director circle preserve mechanics", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.necron") && f.Engine.Arena!.Contains(new(117.9f, 0, 114.9f))
                && !f.Engine.Arena.Contains(new(118.1f, 0, 100)), "Identity/initial rectangle.");
            Start(f, 1, 44532); End(f, 1, 44532); Check(Group(f, "memento").Any(), "Memento ended on cast.");
            f.Engine.MapEffect(0x18, 0x00020001); f.Render();
            Check(!Group(f, "memento").Any() && !f.Engine.Arena!.Contains(new(100, 0, 100))
                && f.Engine.Arena.Contains(new(88, 0, 100)) && f.Engine.Arena.Contains(new(112, 0, 100)), "Split geometry.");
            f.Engine.MapEffect(0x18, 0x00080004); f.Render(); Check(f.Engine.Arena!.Contains(new(100, 0, 100)), "Split restore.");
            Start(f, 10, 44603);
            Check(AtTime(f, Group(f, "shrink").Single().Activation, 6.4), "Shrink visual finish plus delay.");
            f.Events.Handle(new LogSystemDirectorUpdateEvent { Command = 0x8000000D, Param1 = 2, Timestamp = f.Now }, f.Now); f.Render();
            Check(!Group(f, "shrink").Any() && f.Engine.Arena.Contains(new(108.9f, 0, 100))
                && !f.Engine.Arena.Contains(new(109.1f, 0, 100)), "Director Param1 lost before native runtime.");
            f.Engine.DirectorUpdate(0x8000000D, 1); f.Render(); Check(f.Engine.Arena.Contains(new(117, 0, 114)), "Director restore.");
        });
        run("Necron fear starts independent eight and thirteen point seven clocks for existing and new hands", () =>
        {
            using var f = Scene(); Start(f, 1, 44521);
            Check(Group(f, "hand").Single().Activation == f.Start.AddSeconds(13.7)
                && Group(f, "fear").Single().Activation == f.Start.AddSeconds(8), "Existing hand clocks.");
            At(f, 2); f.Actors[31] = new(31, 0x4903, new(90, 0, 95), Rotation: .6f); f.Scan();
            Check(Group(f, "hand").Count() == 2 && Group(f, "hand").Any(a => a.Activation == f.Start.AddSeconds(15.7))
                && Group(f, "fear").Any(a => a.Activation == f.Start.AddSeconds(10)), "New hand creation clocks.");
            Start(f, 10, 44522); End(f, 10, 44522);
            Check(!Group(f, "fear").Any() && Group(f, "hand").Count() == 2, "Fear resolution affected hand stream.");
            Start(f, 30, 44523); End(f, 30, 44523); Check(!Group(f, "hand").Any(), "IcyHands1 finish did not clear entire hand stream.");
        });
        run("Necron choking non special hands append actual casts and consume FIFO only", () =>
        {
            using var f = Scene(); Start(f, 1, 44532);
            Check(Group(f, "hand").Single().Activation == f.Start.AddSeconds(11.1), "Memento hand clock.");
            Start(f, 10, 44523); Start(f, 11, 44523); End(f, 11, 44523);
            Check(Group(f, "hand").Length == 2 && Group(f, "hand").All(a => a.Activation == f.Start.AddSeconds(5.3)), "Helper finish selected source instead of FIFO.");
            At(f, 100); Check(Group(f, "hand").Length == 2 && Group(f, "memento").Any(), "Time incorrectly cleared persistent mechanisms.");
        });
        run("Necron cold grip three slots use visual order and final shifted then corrected position", () =>
        {
            using var f = Scene(); Start(f, 1, 44524); Start(f, 10, 44611); Start(f, 11, 44611);
            Check(Group(f, "cold").Length == 3 && Group(f, "cold")[2].Position.X == 107
                && AtTime(f, Group(f, "cold")[2].Activation, 6.9), "Cold third prediction.");
            End(f, 10, 44611); Check(Group(f, "cold").Length == 2 && Group(f, "cold")[1].Position.X == 107, "First cold finish.");
            End(f, 11, 44611); Check(MathF.Abs(Group(f, "cold").Single().Position.X - 106) < .04f, "Last cold correction.");
            Start(f, 12, 44526); End(f, 12, 44526); Check(!Group(f, "cold").Any(), "Final cold finish.");
        });
        run("Necron cold visual arriving after two helpers uses two point six and negative offset", () =>
        {
            using var f = Scene(); Start(f, 10, 44611); Start(f, 11, 44611); Start(f, 1, 44525, rotation: MathF.PI / 2);
            Check(Group(f, "cold").Length == 3 && Group(f, "cold")[2].Position.X == 93
                && AtTime(f, Group(f, "cold")[2].Activation, 7.9)
                && Group(f, "cold")[2].Rotation == MathF.PI / 2, "Late visual offset/time/rotation.");
        });
        run("Necron ordinary blight icon FIFO instantaneous preview hide show finish plus one point two", () =>
        {
            using var f = Scene(); Icon(f, 604); Icon(f, 605);
            Check(Group(f, "blight").Single(a => a.Display).Shape is CircleTemplate { Radius: 20 }
                && Group(f, "blight/prepare").Any(), "Icon queue/next donut AI.");
            Start(f, 1, 44527); Check(!Group(f, "blight").Any(), "Soul reaping did not hide both views and navigation.");
            At(f, 1); Start(f, 1, 44528);
            Check(AtTime(f, Group(f, "blight").Single(a => a.Display).Activation, 7.5), "Blight finish offset.");
            Start(f, 10, 45181); End(f, 10, 45181); Check(Group(f, "blight").Single(a => a.Display).Shape is DonutTemplate { InnerRadius: 16, OuterRadius: 60 }, "Blight finish FIFO.");
            Start(f, 11, 45182); End(f, 11, 45182); Check(!Group(f, "blight").Any(), "Blight final finish.");
        });
        run("Necron relentless stores four hints rotates on any model state and emits one timed item", () =>
        {
            using var f = Scene(); Start(f, 1, 44531); Icon(f, 604); Icon(f, 605); Icon(f, 621); Icon(f, 622);
            Check(!Group(f, "blight").Any(), "Relentless icons produced premature AOEs.");
            f.Actors[12] = f.Actors[12] with { ModelState = 147 }; f.Scan();
            Check(Group(f, "blight").Single(a => a.Display).Shape is DonutTemplate
                && Group(f, "blight").Single(a => a.Display).Activation == f.Start.AddSeconds(14.4), "Model rotation index1/time.");
            var position = Group(f, "blight").Single(a => a.Display).Position;
            f.Actors[12] = f.Actors[12] with { ModelState = 22, Position = new(90, 0, 90) }; f.Scan();
            Check(Group(f, "blight").Single(a => a.Display).Position == position, "Model changes rotated twice.");
            Start(f, 10, 45182); End(f, 10, 45182);
            Check(Group(f, "blight").Single(a => a.Display).Shape is CircleTemplate
                && Group(f, "blight").Single(a => a.Display).Activation == f.Start.AddSeconds(17.2), "Relentless queue interval.");
        });
        run("Necron grand cross tethers accept arbitrary ids swap first two only and replace first prediction", () =>
        {
            using var f = Scene();
            f.Actors[32] = new(32, 0x4948, new(90, 0, 100));
            f.Actors[33] = new(33, 0x490A, new(110, 0, 100)); f.Scan();
            f.Engine.Tether(100, 999, true, f.Now, 32); f.Engine.Tether(100, 123, true, f.Now, 33); f.Render();
            Check(Group(f, "rect").Length == 2 && Group(f, "rect")[0].Activation == f.Start.AddSeconds(5.6)
                && Group(f, "rect")[0].Shape is RectangleTemplate { Width: 4.5f, Length: 100 }, "Predicted bidirectional rectangle/order.");
            Start(f, 10, 44534); Check(Group(f, "rect")[0].Shape is RayRectangleTemplate { Length: 100 }, "Actual did not replace first prediction.");
            End(f, 10, 44534); Check(Group(f, "rect").Single().Activation == f.Start.AddSeconds(7.6), "Actual FIFO finish.");
        });
        run("Necron invitation any tether stores source snapshot and actual finish FIFO", () =>
        {
            using var f = Scene(); f.Actors[32] = new(32, 0x49DD, new(95, 0, 100), Rotation: .7f); f.Scan();
            f.Engine.Tether(100, 1, true, f.Now, 32); f.Render();
            Check(Group(f, "invitation").Single().Activation == f.Start.AddSeconds(12.3), "Invitation tether.");
            var snapshot = Group(f, "invitation").Single().Position;
            f.Actors[32] = f.Actors[32] with { Position = new(110, 0, 100) }; f.Scan();
            Check(Group(f, "invitation").Single().Position == snapshot, "Invitation snapshot moved.");
            Start(f, 10, 44817); End(f, 10, 44817); Check(!Group(f, "invitation").Any(), "Invitation finish.");
        });
        run("Necron delayed raidwides have independent visual finish offsets and real hit endings no TTL", () =>
        {
            using var f = Scene(); Start(f, 1, 44541); Start(f, 10, 44538);
            Check(f.Engine.PredictedDamage.Count() == 16 && f.Engine.PredictedDamage.Any(p => AtTime(f, p.Activation, 11.7))
                && f.Engine.PredictedDamage.Any(p => AtTime(f, p.Activation, 7.9)), "Independent delay activations.");
            End(f, 1, 44541); End(f, 10, 44538); At(f, 100);
            Check(f.Engine.PredictedDamage.Count() == 16, "Cast/time consumed delayed damage.");
            Hit(f, 11, 44542, 1); Check(f.Engine.PredictedDamage.Count() == 8, "Darkness hit consumed neutron.");
            Hit(f, 11, 44539, 2); Check(!f.Engine.PredictedDamage.Any(), "Neutron hit failed clear.");
        });
        run("Necron shockwave icon target fallback first death clears all later death retained effect FIFO", () =>
        {
            using var f = Scene(); f.Engine.Icon(999, 615, f.Now, 101); f.Engine.Icon(102, 615, f.Now, 1); f.Render();
            Check(Group(f, "shockwave").Count(a => a.Display) == 2 && f.Engine.PredictedDamage.Count() == 2
                && f.Engine.PredictedDamage.All(p => p.DamageType == "Raidwide" && p.Activation == f.Start.AddSeconds(7.2)), "Bait source/fallback prediction.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            Check(Group(f, "shockwave").Count(a => a.Display) == 2, "Override incorrectly used base death cleanup.");
            Hit(f, 10, 44547, 1); Check(Group(f, "shockwave").Count(a => a.Display) == 1, "Effect should only consume one before Update.");
            f.Scan(); Check(!Group(f, "shockwave").Any(), "New first dead target failed clear.");
            f.Engine.Icon(100, 615, f.Now, 1); f.Actors[100] = f.Actors[100] with { Position = new(100, -101, -100) }; f.Scan();
            Check(!Group(f, "shockwave").Any(), "First target prison teleport failed clear.");
        });
        run("Necron prison exact four disconnected islands offset center three directed edges and soft exit", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Position = new(100, -101, -100) }; f.Scan();
            var arena = f.Engine.Arena!;
            Check(arena.Position == new Vector3(106.875f, -101, -108) && arena.GetContours().Count == 4
                && arena.Contains(new(100, -101, -100)) && arena.Contains(new(95, -101, -121))
                && !arena.Contains(new(100, -101, -115)) && f.Engine.NativeTeleporters.Count == 0, "Prison arena without active portals.");
            f.Engine.MapEffect(0, 0x00020001); f.Render();
            Check(f.Engine.NativeTeleporters.Count == 3 && f.Engine.NativeTeleporters.All(t => !t.Bidirectional && t.Radius == 2 && t.UseTimeSeconds == 0)
                && Group(f, "prison").Count(a => a.Display) == 4 && Group(f, "prison/goal").Single() is { Safe: true, GoalPriority: 9, Shape: CircleTemplate { Radius: 1 } }, "Prison navigation vs drawing.");
            Check(f.Engine.NativeTeleporters[0].Entrance == new Vector3(100, -101, -107.4f)
                && f.Engine.NativeTeleporters[0].Exit == new Vector3(94, -101, -118), "Teleporter coordinates.");
            f.Engine.MapEffect(0, 0x00080004); f.Render(); Check(f.Engine.NativeTeleporters.Count == 0 && !Group(f, "prison").Any(), "Portal loss cleanup.");
        });
        run("Necron prison viewpoint ground exact height grand cross precedence and all eight map bits", () =>
        {
            using var f = Scene(); f.Engine.DirectorUpdate(0x8000000D, 2);
            for (var i = 0; i < NecronArenas.PRISONS.Length; ++i)
            {
                var p = NecronArenas.PRISONS[i]; f.Actors[100] = f.Actors[100] with { Position = new(p.X, -101, p.Y) }; f.Scan();
                f.Engine.MapEffect((uint)i, 0x00020001); f.Render();
                Check(f.Engine.NativeTeleporters.Count == 3 && f.Engine.Arena!.Contains(f.Actors[100].Position), "Prison bit selection.");
            }
            f.Actors[100] = f.Actors[100] with { Position = new(100, -100, 100) }; f.Scan();
            Check(f.Engine.NativeTeleporters.Count == 0 && f.Engine.Arena!.Contains(new(108, -100, 100))
                && !f.Engine.Arena.Contains(new(110, -100, 100)), "Y equality/GrandCross main arena restore.");
        });
        run("Necron towers duplicate indices four additions cap independent removal and first activation", () =>
        {
            using var f = Scene();
            f.Engine.MapEffect(0x1A, 0x00020001); At(f, 1); f.Engine.MapEffect(0x1A, 0x00020001);
            f.Engine.MapEffect(0x1B, 0x00020001); f.Engine.MapEffect(0x1C, 0x00020001); f.Engine.MapEffect(0x1D, 0x00020001); f.Render();
            Check(Group(f, "tower").Count(a => a.Display) == 4 && Group(f, "tower/join").Single().Activation == f.Start.AddSeconds(15), "Tower map dedup/cap or clock.");
            f.Engine.MapEffect(0x1A, 0x08000400); f.Engine.MapEffect(0x1D, 0x00020001); f.Render();
            Check(Group(f, "tower").Count(a => a.Display) == 3, "Counter decremented per removal.");
            f.Engine.MapEffect(0x1A, 0x00080004); f.Engine.MapEffect(0x1B, 0x00080004); f.Engine.MapEffect(0x1C, 0x00080004);
            f.Engine.MapEffect(0x1D, 0x00020001); f.Render();
            Check(Group(f, "tower").Count(a => a.Display) == 1, "Last removal failed reset counter.");
        });
        run("Necron tower prioritize algorithm picks occupied candidate skips own sole tower and predicts occupants", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = new(94, 0, 90) };
            f.Actors[101] = f.Actors[101] with { Position = new(106, 0, 90) }; f.Scan();
            f.Engine.MapEffect(0x1A, 0x00020001); f.Engine.MapEffect(0x1B, 0x00020001); f.Render();
            var join = Group(f, "tower/join").Single(); var geometry = join.Shape.Resolve(join.Position, join.Rotation, null);
            Check(geometry.Contains(new(94, 0, 90)) && !geometry.Contains(new(106, 0, 90))
                && f.Engine.PredictedDamage.Count() == 2, "Tower priority/join complement or prediction.");
        });
        run("Necron DungeonOutput publishes directed portals and clears publisher on suspension", () =>
        {
            using var f = Scene(); IReadOnlyList<SolverTeleporter> edges = [];
            using var safe = new MasterPlayer.Scripting.ScriptSafeZoneApi();
            var output = new DungeonOutput(safe, new(), safe.Manager, value => edges = value);
            f.Actors[100] = f.Actors[100] with { Position = new(100, -101, -100) }; f.Scan();
            f.Engine.MapEffect(0, 0x00020001); output.Apply(f.Engine, f.Now, false);
            Check(edges.Count == 3, "Output did not publish portals.");
            output.Clear(); Check(edges.Count == 0, "Output clear did not revoke portals.");
        });
        run("Necron director wipe clears outputs blocks stale same combat and allows next pull", () =>
        {
            using var f = Scene(); Start(f, 1, 44521); f.Engine.DirectorUpdate(0x80000029); f.Render(); f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Engine.NativeTeleporters.Count == 0, "Wipe immediately restarted stale encounter.");
            f.Actors[1] = f.Actors[1] with { InCombat = false, Cast = null }; f.Scan();
            f.Actors[1] = f.Actors[1] with { InCombat = true }; f.Scan(); Check(f.Engine.Active != null, "Next pull blocked.");
        });
        run("Necron missing primary preserves stored fight and leaving prison restores full rectangle", () =>
        {
            using var f = Scene(); Icon(f, 604); f.Engine.Icon(101, 615, f.Now, 1); f.Engine.MapEffect(0x18, 0x00020001);
            f.Actors[100] = f.Actors[100] with { Position = new(100, -101, -100) }; f.Scan();
            f.Engine.MapEffect(0, 0x00020001); f.Actors.Remove(1); f.Scan();
            Check(f.Engine.Active != null && Group(f, "blight").Any() && Group(f, "shockwave").Any() && f.Engine.NativeTeleporters.Count == 3,
                "Primary snapshot absence cleared live mechanics.");
            f.Engine.RemoveActor(1); f.Render();
            Check(f.Engine.Active != null && Group(f, "blight").Any() && Group(f, "shockwave").Any(), "Destroyed primary ended active phase.");
            f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 100) }; f.Scan();
            Check(f.Engine.Arena!.Contains(new(100, 0, 100)) && f.Engine.NativeTeleporters.Count == 0,
                "Leaving prison restored stale split arena.");
        });
        run("Necron ordinary shapes no unimplemented mechanics no controls personal death and boss cleanup", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 44540u, 44536u, 44535u })
            { Start(f, 10, action); Check(f.Engine.OutputCasts.Any() && f.Draw.Previews.Any(), "Ordinary shape."); End(f, 10, action); At(f, (f.Now - f.Start).TotalSeconds + 1); }
            Start(f, 1, 44521); f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.Active != null && !f.Engine.RequireAllStop && !f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Unexpected personal reset or controls.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Engine.NativeTeleporters.Count == 0, "Primary cleanup.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x4870, 1295); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i,
            PartySlot: i, PartyRole: i < 2 ? "Tank" : "Melee", Hostile: false);
        for (ulong i = 10; i < 30; ++i) f.Actors[i] = new(i, 0x233C, c);
        f.Actors[30] = new(30, 0x4903, new(90, 0, 85)); f.Scan(); return f;
    }
    private static void At(Fight f, double seconds) { f.Now = f.Start.AddSeconds(seconds); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, double duration = 5, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static void Hit(Fight f, ulong source, uint action, uint sequence)
    { f.Engine.Effect(source, action, sequence, f.Now); f.Render(); }
    private static void Icon(Fight f, uint icon) { f.Engine.Icon(1, icon, f.Now, 1); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("necron/" + prefix)).ToArray();
    private static bool AtTime(Fight f, DateTime? actual, double seconds) => actual is { } at && Math.Abs((at - f.Start.AddSeconds(seconds)).Ticks) <= 10;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
