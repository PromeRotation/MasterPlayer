using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class QueenEternalRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("QueenEternal native identity map X holes disconnected split gravity and final timeline", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.queen-eternal") && f.Engine.Arena!.Contains(new(119.9f, 0, 119.9f)), "Identity/default.");
            f.Engine.MapEffect(0, 0x00020001);
            Check(f.Engine.Arena!.Position.Z == 92.5f && f.Engine.Arena.Contains(new(100, 0, 92.5f))
                && !f.Engine.Arena.Contains(new(100, 0, 86)), "X contours/holes.");
            f.Engine.MapEffect(2, 0x00020001);
            Check(!f.Engine.Arena.Contains(new(100, 0, 94)) && f.Engine.Arena.Contains(new(108, 0, 94)), "Split islands.");
            Status(f, 101, 3814, true, 20); f.Scan();
            Check(f.Engine.Arena.Contains(new(100, 0, 94)), "Gravity was not global across party.");
            Status(f, 101, 3814, false); f.Scan(); Check(!f.Engine.Arena.Contains(new(100, 0, 94)), "Gravity removal.");
            f.Engine.Timeline(10, 0x2BFE, f.Now);
            Check(f.Engine.Arena.Position.Z == 105 && f.Engine.Arena.Contains(new(119, 0, 119)), "Final timeline any actor.");
            f.Engine.MapEffect(1, 0x00040004); Check(f.Engine.Arena.Position.Z == 100, "Default reset state.");
        });
        run("QueenEternal legitimate force directional two hits same side hides second and effect only consumes FIFO", () =>
        {
            using var f = Scene(); Start(f, 1, 36639, rotation: .5f);
            var items = Group(f, "force").Where(a => a.Display).ToArray();
            Check(items.Length == 2 && items[0].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -40, MaxLocalX: 40 }
                && items[0].Rotation == .5f - MathF.PI / 2 && items[1].Rotation == .5f + MathF.PI / 2
                && Time(f, items[1].Activation, 8.4), "Directional sequence.");
            End(f, 1, 36639); At(f, 40); Check(Group(f, "force").Count(a => a.Display) == 2, "Finish/timeout consumed effect-ended queue.");
            Hit(f, 10, 36639, 1); Check(Group(f, "force").Count(a => a.Display) == 1, "First hit.");
            Hit(f, 10, 36643, 2); Check(!Group(f, "force").Any(), "Second hit.");
            At(f, 41); Start(f, 1, 36638); Check(Group(f, "force").Count(a => a.Display) == 1, "Repeated same side second should stay hidden.");
        });
        run("QueenEternal besiegement map generation changes only at second valid map hides force then hit clears", () =>
        {
            using var f = Scene(); Start(f, 1, 36641);
            f.Engine.MapEffect(3, 0x00200010); f.Render();
            Check(Group(f, "besiege").Length == 4 && Group(f, "besiege")[0].Shape is RayRectangleTemplate { MinLocalX: -6, MaxLocalX: 6 }
                && Time(f, Group(f, "besiege")[0].Activation, 6.6) && !Group(f, "force").Any(a => a.Display), "First map/draw suppression.");
            Hit(f, 10, 36614, 1); Check(Group(f, "force").Any(a => a.Display), "Besiege hit did not restore slashes.");
            f.Engine.MapEffect(3, 0x02000100); f.Render(); Check(!Group(f, "besiege").Any(), "Second-generation excluded map added lanes.");
            f.Engine.MapEffect(3, 0x00200010); f.Render();
            Check(Group(f, "besiege")[0].Shape is RayRectangleTemplate { MinLocalX: -4, MaxLocalX: 4 }, "Second generation widths.");
            Hit(f, 10, 36618, 2); f.Engine.MapEffect(3, 0x08000400); f.Render();
            Check(Group(f, "besiege").Length == 3, "Generation counter stayed at second forever.");
        });
        run("QueenEternal restored force packet correction updates remaining hits without duplicating consumed hits", () =>
        {
            using var f = Scene();
            var raw = new DungeonCastObservation(36639, f.Actors[1].Position, 0, f.Now, f.Now.AddSeconds(5.3));
            f.Actors[1] = f.Actors[1] with { Cast = raw }; f.Scan();
            f.Engine.StartCast(1, raw with { Rotation = .6f }, f.Now); f.Render();
            Check(Group(f, "force").Count(a => a.Display) == 2
                && Group(f, "force").First().Rotation == .6f - MathF.PI / 2, "Snapshot correction duplicated force.");
            Hit(f, 10, 36639, 1);
            f.Engine.StartCast(1, raw with { Rotation = .8f }, f.Now); f.Render();
            Check(Group(f, "force").Count(a => a.Display) == 1
                && Group(f, "force").First().Rotation == .8f + MathF.PI / 2, "Late correction restored consumed hit.");
            Hit(f, 10, 36643, 2); Check(!Group(f, "force").Any(), "Corrected force queue retained duplicate hits.");
        });
        run("QueenEternal adapter predictions use current processing frame for delayed icon map and timeline", () =>
        {
            using var f = Scene(); f.Now = f.Start.AddSeconds(10);
            f.Events.Handle(new LogSystemTargetIconEvent { SourceId = 100, TargetId = 100, MarkerId = 327, Timestamp = f.Start }, f.Now);
            f.Events.Handle(new LogSystemMapEffectEvent { Position = 3, Param1 = 0x0010, Param2 = 0x0020, Timestamp = f.Start }, f.Now);
            f.Events.Handle(new LogSystemPlayActionTimelineEvent { SourceId = 20, TimelineId = 0x11D7, Timestamp = f.Start }, f.Now);
            Check(Time(f, f.Engine.PredictedDamage.Single().Activation, 16)
                && Time(f, Group(f, "besiege").First().Activation, 16.6)
                && Time(f, Group(f, "waltz").Single().Activation, 17), "Delayed event used stale packet/scan clock.");
        });
        run("QueenEternal aethertithe three map angles actual hit clear with independent state", () =>
        {
            using var f = Scene();
            foreach (var (state, degrees) in new[] { (0x04000100u, -55f), (0x08000100u, 0f), (0x10000100u, 55f) })
            {
                f.Engine.MapEffect(0, state); f.Render();
                Check(Group(f, "tithe").Single().Rotation == degrees * MathF.PI / 180
                    && Time(f, Group(f, "tithe").Single().Activation, 5), "Tithe map angle/clock.");
            }
            Hit(f, 10, 36658, 1); Check(!Group(f, "tithe").Any(), "Tithe effect.");
        });
        run("QueenEternal waltz timeline filters oid dynamic target positional consumption and destruction", () =>
        {
            using var f = Scene(); f.Engine.Timeline(20, 0x11D7, f.Now); f.Engine.Timeline(10, 0x11D7, f.Now); f.Render();
            Check(Group(f, "waltz").Length == 1 && Time(f, Group(f, "waltz").Single().Activation, 7), "Waltz timeline filtering/time.");
            f.Actors[20] = f.Actors[20] with { Position = new(110, 0, 105) }; f.Scan();
            Check(Group(f, "waltz").Single().Position.X == 110, "Bait marker did not follow.");
            Hit(f, 10, 36631, 1); Check(Group(f, "waltz").Any(), "Unmatched visual consumed.");
            f.Actors[10] = f.Actors[10] with { Position = new(110.4f, 0, 105.4f) }; f.Scan(); Hit(f, 10, 36631, 2);
            Check(!Group(f, "waltz").Any(), "Positional match.");
            f.Engine.Timeline(20, 0x11D7, f.Now); f.Engine.RemoveActor(20); f.Render(); Check(!Group(f, "waltz").Any(), "Marker destruction.");
        });
        run("QueenEternal ruthless timeline dynamic orientation arbitrary tether AI uses source radians as degrees", () =>
        {
            using var f = Scene(); f.Engine.Timeline(21, 0x11D2, f.Now); f.Render();
            Check(Group(f, "ruthless").Length == 2 && Time(f, Group(f, "ruthless/avoid").Single().Activation, 11.1), "Ruthless timeline.");
            f.Engine.Tether(21, 999, true, f.Now, 100); f.Engine.Tether(21, 123, true, f.Now, 101);
            f.Actors[21] = f.Actors[21] with { Rotation = 1.5f }; f.Scan();
            var tether = Group(f, "ruthless/tether").Single();
            Check(tether.Activation == null && tether.Rotation == 1.5f * MathF.PI / 180
                && tether.Position.Z == f.Actors[101].Position.Z, "Tether-specific source semantics.");
            f.Engine.Tether(21, 888, false, f.Now, 102); f.Render(); Check(Group(f, "ruthless/avoid").Any(), "Any untether should clear full list.");
            Hit(f, 10, 36634, 1); Check(!Group(f, "ruthless").Any(), "Ruthless effect.");
        });
        run("QueenEternal royal banishment time window uses insertion first strict one second and caster removal", () =>
        {
            using var f = Scene(); Start(f, 10, 36647, duration: 5); Start(f, 11, 36647, duration: 5.9); Start(f, 12, 36647, duration: 6);
            Check(Group(f, "banishment").Length == 2, "Time-window strict upper bound.");
            End(f, 10, 36647); Check(Group(f, "banishment").Length == 2, "Caster finish failed publish next group.");
            End(f, 12, 36647); Check(Group(f, "banishment").Length == 1, "Finish selected FIFO instead of caster.");
        });
        run("QueenEternal divide and conquer icon cannot reset active wave eighth hit and persistent cast counter", () =>
        {
            using var f = Scene(); Icon(f, 1, 521);
            Check(Group(f, "line").Count(a => a.Display) == 8, "Full-party baits.");
            for (uint i = 1; i <= 7; ++i) Hit(f, 10, 36637, i);
            Icon(f, 1, 521); Check(Group(f, "line").Count(a => a.Display) == 8, "Active wave reset.");
            Hit(f, 10, 36637, 8); Check(!Group(f, "line").Any(), "Eight effects.");
            Icon(f, 1, 521); Hit(f, 10, 36637, 9); Check(!Group(f, "line").Any(), "Source ninth cumulative hit resets NumCasts.");
        });
        run("QueenEternal flare icons predict first activation remove dead targets permanently and consume FIFO", () =>
        {
            using var f = Scene(); Icon(f, 100, 327); At(f, 1); Icon(f, 101, 327);
            Check(Group(f, "flare").Count(a => a.Display) == 2 && f.Engine.PredictedDamage.Count() == 2
                && f.Engine.PredictedDamage.All(p => Time(f, p.Activation, 6)), "Flare first activation.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan(); Check(Group(f, "flare").Count(a => a.Display) == 1, "Dead target not removed.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan(); Check(Group(f, "flare").Count(a => a.Display) == 1, "Revival restored consumed flare.");
            Icon(f, 102, 327); Hit(f, 10, 39518, 1);
            Check(Group(f, "flare").Count(a => a.Display) == 1 && f.Engine.PredictedDamage.Single().TargetId == 102, "Flare hit did not consume FIFO.");
            Hit(f, 10, 39518, 2); Check(!Group(f, "flare").Any() && !f.Engine.PredictedDamage.Any(), "Final flare hit.");
        });
        run("QueenEternal dorito retargets nearest safe party including dead fallback player and preserves time", () =>
        {
            using var f = Scene(); Icon(f, 102, 55); f.Scan();
            Check(Group(f, "dorito/display").Single().Position.X is > 100.9f and < 101.1f, "Nearest player replacement.");
            Start(f, 10, 36622, location: new(101, 0, 100)); f.Scan();
            Check(Group(f, "dorito/display").Single().Position.X > 108, "AOE-covered teammate selected.");
            Hit(f, 10, 39519, 1); Check(!Group(f, "dorito").Any(), "Dorito hit.");
        });
        run("QueenEternal gaze shared last status activation ten second display and PR facing calculation", () =>
        {
            using var f = Scene(); Status(f, 101, 3815, true, 20); Status(f, 102, 3815, true, 21);
            Check(!f.Engine.Gazes.Any(), "Eye shown too early.");
            At(f, 11); Check(f.Engine.Gazes.Count() == 2 && f.Engine.Gazes.All(g => Time(f, g.FinishAt, 21)), "Shared gaze expiry.");
            At(f, 20.3); var facing = f.Engine.GazeFacing(f.Actors[100].Position, MathF.PI / 2);
            Check(facing != null && MathF.Abs(facing.Value - MathF.PI / 2) > .1f, "PR facing not planned.");
            Status(f, 101, 3815, false); Check(f.Engine.Gazes.Count() == 1, "One eye loss cleared both.");
        });
        run("QueenEternal hold packet adapter pending raw confirmation release and current player isolation", () =>
        {
            using var f = Scene();
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 4130, Duration = 5, Timestamp = f.Now }, f.Now);
            At(f, 4); Check(f.Engine.RequireAllStop, "Status whitelist/one-second threshold.");
            f.PlayerId = 101; f.Scan(); Check(!f.Engine.RequireAllStop, "Viewpoint retained old hold.");
            f.PlayerId = 100; f.Scan(); Check(f.Engine.RequireAllStop, "Viewpoint erased per-player history.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 4130, Timestamp = f.Now }, f.Now);
            Check(!f.Engine.RequireAllStop, "Hold remove packet.");
        });
        run("QueenEternal native eye and view arc change safety color follow actors and remain display only", () =>
        {
            using var f = Scene(); Status(f, 101, 3815, true, 5);
            f.Actors[100] = f.Actors[100] with { Rotation = MathF.PI / 2 }; f.Scan();
            var eye = Group(f, "gaze/0/eye").Single();
            Check(eye.Color is { X: 1 } && eye.Unclipped && !eye.Navigate
                && Group(f, "gaze/view").Single().Shape is LocalPolygonTemplate
                && f.Draw.Previews.ContainsKey("queen/gaze/0/pupil") && f.Snapshot.DangerAreas.Count == 0, "Danger eye or display-only arc missing.");
            f.Actors[100] = f.Actors[100] with { Rotation = -MathF.PI / 2 };
            f.Actors[101] = f.Actors[101] with { Position = new(106, 0, 100) }; f.Scan();
            Check(Group(f, "gaze/0/eye").Single() is { Position.X: 106, Color.Y: 1, Safe: true }
                && f.Draw.Previews["queen/gaze/0/eye"].Color?.Y == 1, "Safe eye did not follow target/heading.");
            var arc = Group(f, "gaze/view").Single();
            var contour = arc.Shape.Resolve(arc.Position, arc.Rotation, null).GetContours().Single();
            var parts = MasterPlayer.Radar.RadarClipRegion.ConvexParts(contour.Select(p => new Vector2(p.X, p.Z)).ToArray()).ToArray();
            Check(parts.Length > 0 && parts.All(p => p.Count == 3), "Concave view arc could not triangulate without arena clipping.");
            Status(f, 101, 3815, false); Check(!Group(f, "gaze").Any()
                && !f.Draw.Previews.Keys.Any(k => k.StartsWith("queen/gaze")), "Eye previews leaked after loss.");
        });
        run("QueenEternal gaze parameter refresh appends same actor and source change loses first before gain", () =>
        {
            using var f = Scene();
            f.Engine.StatusChanged(101, 3815, true, f.Now, param: 1, expiresAt: f.Start.AddSeconds(5), sourceId: 1);
            f.Engine.StatusChanged(101, 3815, true, f.Now, param: 2, expiresAt: f.Start.AddSeconds(5), sourceId: 1);
            Check(f.Engine.Gazes.Count() == 2, "Same-source param refresh was deduplicated.");
            f.Engine.StatusChanged(101, 3815, true, f.Now, param: 2, expiresAt: f.Start.AddSeconds(5), sourceId: 10);
            Check(f.Engine.Gazes.Count() == 2, "Source change did not synthesize Lose before Gain.");
            f.Actors[101] = f.Actors[101] with { Statuses = [3815],
                StatusParams = new Dictionary<uint, ushort> { [3815] = 2 },
                StatusSources = new Dictionary<uint, ulong> { [3815] = 10 },
                StatusExpirations = new Dictionary<uint, DateTime> { [3815] = f.Start.AddSeconds(5) } }; f.Scan();
            Check(f.Engine.Gazes.Count() == 2, "Raw confirmation duplicated packet Gain.");
            f.Engine.RemoveActor(101); f.Render();
            Check(f.Engine.Gazes.Count() == 1, "Destroy must emit one StatusLose, preserving source duplicate semantics.");
        });
        run("QueenEternal knockback AI polygon margins crossing test immunity and real path previews", () =>
        {
            using var f = Scene(); f.Engine.MapEffect(0, 0x00020001); Start(f, 10, 36610, location: new(100, 0, 92.5f));
            Check(Group(f, "knockback/36610/avoid").Any() && Group(f, "knockback").Any(a => a.Display), "Knockback navigation/drawing.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(20) } }; f.Scan();
            Check(!Group(f, "knockback").Any(), "Immunity failed suppress.");
            End(f, 10, 36610); Check(!Group(f, "knockback").Any(), "Knockback finish.");
        });
        run("QueenEternal ordinary casts damage personal death and primary death cleanup", () =>
        {
            using var f = Scene(); Start(f, 10, 36633); Check(f.Engine.OutputCasts.Any() && f.Draw.Previews.Count > 0, "Donut.");
            Start(f, 11, 36602, target: 100); Start(f, 12, 39531);
            Check(f.Engine.PredictedDamage.Count() == 9, "Tankbuster/raidwide predictions.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(f.Engine.Active != null, "Personal death.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan(); Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Primary death.");
        });
        run("QueenEternal knockback immunity gain and loss packets precede raw snapshot with strict expiration", () =>
        {
            using var f = Scene(); f.Engine.MapEffect(0, 0x00020001); Start(f, 10, 36610);
            f.Engine.StatusChanged(100, 160, true, f.Now, expiresAt: f.Start.AddSeconds(5.3));
            Check(Group(f, "knockback").Any(), "Immunity ending at knockback must not suppress it.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 160, Duration = 6, Timestamp = f.Now }, f.Now);
            Check(!Group(f, "knockback").Any(), "Gain packet ignored until raw snapshot.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160],
                StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(6) } }; f.Scan();
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 160, Timestamp = f.Now }, f.Now);
            Check(Group(f, "knockback").Any(), "Loss packet was overridden by stale raw immunity.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x41C0, 1202); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * (i == 7 ? 12 : i),
            PartySlot: i, PartyRole: i < 2 ? "Tank" : "Melee", Hostile: false);
        for (ulong i = 10; i < 20; ++i) f.Actors[i] = new(i, 0x233C, c);
        f.Actors[20] = new(20, 0x41C4, c); f.Actors[21] = new(21, 0x41C2, c); f.Scan(); return f;
    }
    private static void At(Fight f, double seconds) { f.Now = f.Start.AddSeconds(seconds); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, double duration = 5, float rotation = 0, ulong target = 0, Vector3? location = null)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    { f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render(); }
    private static void Hit(Fight f, ulong source, uint action, uint seq) { f.Engine.Effect(source, action, seq, f.Now); f.Render(); }
    private static void Icon(Fight f, ulong actor, uint icon) { f.Engine.Icon(actor, icon, f.Now, actor); f.Render(); }
    private static void Status(Fight f, ulong actor, uint status, bool present, double expires = 0)
    { f.Engine.StatusChanged(actor, status, present, f.Now, expiresAt: f.Start.AddSeconds(expires)); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("queen/" + prefix)).ToArray();
    private static bool Time(Fight f, DateTime? actual, double seconds) => actual is { } at && Math.Abs((at - f.Start.AddSeconds(seconds)).Ticks) <= 10;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
