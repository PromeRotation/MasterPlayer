using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class LakshmiRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Lakshmi tail exact cone and gaze multiple sources PR facing only activation window", () =>
        {
            using var f = Scene();
            f.Cast(10, 9612, 4); var preview = f.Draw.Previews.Single(a => a.Key.StartsWith("cast/")).Value.Geometry;
            Check(preview.Contains(new(0, 0, 6.9f)) && HostDrawingGeometry.Convert(preview).Contains(new(0, 0, 6.9f))
                && !preview.Contains(new(7.1f, 0, 0)), "Tail cone size/angle differs.");
            f.Engine.Finish(10, 9612); f.Actors[10] = f.Actors[10] with { Cast = null };
            f.Cast(10, 9613, 4); f.Render();
            Check(f.Engine.Gazes.Count() == 1 && Group(f, "lakshmi/gaze").Count() == 3, "Gaze eye/pupil/facing drawing missing.");
            f.Actors[100] = f.Actors[100] with { Rotation = MathF.PI }; f.Scan();
            var eye = Group(f, "lakshmi/gaze/10/eye").Single();
            Check(!eye.Safe && eye.Shape is LocalPolygonTemplate
                && HostDrawingGeometry.Convert(eye.Shape.Resolve(eye.Position, eye.Rotation, null)).Contains(eye.Position),
                "Gaze dangerous eye geometry missing from a view.");
            f.Actors[100] = f.Actors[100] with { Rotation = 0 }; f.Scan();
            Check(Group(f, "lakshmi/gaze/10/eye").Single().Safe, "Gaze safe color does not follow actor facing.");
            Check(f.Engine.GazeFacing(new(0, 0, 10), MathF.PI) == null, "Gaze facing armed too early.");
            f.Now = f.Start.AddSeconds(4); f.Scan();
            var heading = f.Engine.GazeFacing(new(0, 0, 10), MathF.PI);
            Check(heading.HasValue && MathF.Abs(MathF.Atan2(MathF.Sin(heading.Value - MathF.PI), MathF.Cos(heading.Value - MathF.PI))) > MathF.PI / 4,
                "Gaze PR heading still faces eye.");
            f.Engine.Finish(10, 9613); f.Actors[10] = f.Actors[10] with { Cast = null }; f.Render();
            Check(f.Engine.GazeFacing(Vector3.Zero, 0) == null && !f.Engine.Gazes.Any(), "Gaze finish leaked.");
        });
        run("Lakshmi separate icon target identities clocks dynamic fixed cross and independent real effects", () =>
        {
            using var f = Scene();
            f.Engine.Icon(101, 14, f.Start, 100);
            f.Engine.Icon(102, 107, f.Start.AddSeconds(.1), 100);
            f.Engine.Icon(100, 109, f.Start.AddSeconds(.2), 101); f.Render();
            var displays = Group(f, "lakshmi/bait").Where(a => a.Display).ToArray();
            Check(displays.Length == 3 && displays.Count(a => a.OutlineOnly) == 1, "Icon targetID/own outlines differ.");
            var path = Group(f, "lakshmi/bait/1/avoid").Single();
            Check(path.Activation == f.Start.AddSeconds((double)4.7f), "Path float activation changed.");
            var cross = Group(f, "lakshmi/bait/2/avoid").Single();
            Check(cross.Rotation == MathF.PI && cross.Activation == f.Start.AddSeconds(.1).AddSeconds(4.7), "Cross rotation/double clock differs.");
            var geometry = cross.Shape.Resolve(cross.Position, cross.Rotation, null);
            Check(geometry.Contains(cross.Position + new Vector3(39, 0, 3.9f))
                && HostDrawingGeometry.Convert(geometry).Contains(cross.Position + new Vector3(3.9f, 0, 39))
                && !geometry.Contains(cross.Position + new Vector3(5, 0, 5)), "Cross dual-view geometry differs.");
            Check(!Group(f, "lakshmi/bait/3").Any(a => a.Navigate), "Own circle got unsupported edge bait navigation.");
            Check(!f.Engine.PredictedDamage.Any(), "GenericBait damageType None acquired TB predictions.");
            f.Actors[102] = f.Actors[102] with { Position = new(7, 0, 8), Rotation = .7f }; f.Scan();
            cross = Group(f, "lakshmi/bait/2/display").Single();
            Check(cross.Position == new Vector3(7, 0, 8) && cross.Rotation == MathF.PI, "Cross froze target or rotated with player.");
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(Group(f, "lakshmi/bait").Count(a => a.Display) == 3, "Timer consumed baits.");
            f.Engine.Effect(1, 9377, 1, f.Now); f.Render();
            Check(!Group(f, "lakshmi/bait/1").Any() && Group(f, "lakshmi/bait").Count(a => a.Display) == 2, "Path effect crossed Blissful.");
            f.Engine.Effect(1, 9365, 2, f.Now); f.Render(); Check(!Group(f, "lakshmi/bait").Any(), "Blissful effect leaked.");
        });
        run("Lakshmi marked protean has buddy cone navigation dead target retained while self baits hide", () =>
        {
            using var f = Scene();
            f.Engine.Icon(100, 14, f.Now); f.Engine.Icon(101, 109, f.Now); f.Render();
            Check(Group(f, "lakshmi/bait/1").Count(a => a.Navigate) == 2
                && Group(f, "lakshmi/bait/1").Where(a => a.Navigate).All(a => a.Shape is FanTemplate { Radius: 100 }),
                "Marked protean lacks buddy cones.");
            f.Actors[100] = f.Actors[100] with { Dead = true };
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            Check(Group(f, "lakshmi/bait/1/display").Any() && !Group(f, "lakshmi/bait/2").Any(), "Source-versus-target death semantics differ.");
            f.Actors.Remove(100); f.Actors.Remove(101); f.Scan();
            Check(Group(f, "lakshmi/bait/1/display").Any(), "Removed target lost retained Path reference.");
        });
        run("Lakshmi strict eight stack real finish independent single target busters", () =>
        {
            using var f = Scene();
            for (var i = 0; i < 8; ++i)
                f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, i == 7 ? Vector3.UnitX * 10 : Vector3.Zero,
                    Hostile: false, PartySlot: i);
            f.Scan(); Start(f, 10, 9361, 100); Start(f, 11, 9362, 101);
            Check(f.Engine.Hints(Vector3.Zero).Contains("八人分摊") && Group(f, "lakshmi/stack/10/display").Any(),
                "Strict eight minimum missing.");
            Check(f.Engine.PredictedDamage.Single().TargetId == 101, "Stack falsely predicted raidwide or buster target lost.");
            f.Actors[107] = f.Actors[107] with { Position = Vector3.Zero }; f.Scan();
            Check(!f.Engine.Hints(Vector3.Zero).Contains("八人分摊"), "Eight members not satisfied.");
            f.Engine.Effect(10, 9361, 1, f.Now, 100); f.Render(); Check(Group(f, "lakshmi/stack").Any(), "Effect replaced cast finish.");
            f.Engine.Finish(10, 9361); f.Actors[10] = f.Actors[10] with { Cast = null }; f.Render();
            Check(!Group(f, "lakshmi/stack").Any() && f.Engine.Casts.ContainsKey(11), "Stack finish cleared buster.");
        });
        foreach (var action in new[] { 9349u, 9374u })
            run($"Lakshmi {action} continuous Vril requests true finish cancellation and fixed knockback", () =>
            {
                using var f = Scene(); f.Cast(1, action, 4);
                Check(f.Engine.ConsumeLakshmiVrilRequest() && f.Engine.ConsumeLakshmiVrilRequest(), "Continuous shield request consumed once.");
                if (action == 9349)
                {
                    var landing = Group(f, "lakshmi/knockback/1/landing").Single();
                    Check(landing.Position.Z == 11.5f && !landing.Navigate, "DivineDenial not fixed 6.5.");
                    f.Actors[100] = f.Actors[100] with { Statuses = [1290] }; f.Scan();
                    Check(Group(f, "lakshmi/knockback/1/landing").Single().Position.Z == 11.5f, "Vril status altered fixed source projection.");
                }
                f.Now = f.Start.AddSeconds(30); f.Scan(); Check(f.Engine.ConsumeLakshmiVrilRequest(), "Timer ended active shield request.");
                f.Engine.Finish(1, action); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Render();
                Check(!f.Engine.ConsumeLakshmiVrilRequest() && !Group(f, "lakshmi/knockback").Any(), "Cancel/finish leaked request.");
            });
        run("Lakshmi Jagadishwari effect requests exactly once independent of shielding cast", () =>
        {
            using var f = Scene();
            f.Engine.Effect(1, 9026, 1, f.Now);
            Check(f.Engine.ConsumeLakshmiVrilRequest() && !f.Engine.ConsumeLakshmiVrilRequest(), "Jagadishwari one-shot differs.");
            f.Cast(1, 9026, 4);
            Check(!f.Engine.ConsumeLakshmiVrilRequest(), "9026 cast incorrectly armed shield.");
            f.Engine.Effect(1, 9026, 2, f.Now);
            f.Engine.ResetFight(); f.Scan(); Check(!f.Engine.ConsumeLakshmiVrilRequest(), "Reset retained one-shot.");
        });
        run("Lakshmi stored charges independent status unknown ownership and exact orb square default solver", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1E21, new(10, 0, 0), Rotation: MathF.PI / 2); f.Scan();
            Check(Group(f, "lakshmi/orb").Single().Shape is CircleTemplate { Radius: .75f }
                && !Group(f, "lakshmi/orb").Any(a => a.Navigate), "Unknown charges treated as zero.");
            f.Engine.DutyActionPlayerId = 100; f.Engine.DutyActions = [new(0, 9345, 0, 1)]; f.Render();
            var goal = Group(f, "lakshmi/orb/20/goal").Single();
            Check(Vector3.Distance(goal.Position, new Vector3(10.5f, 0, 0)) < .000001f
                && goal.Rotation == 0 && goal.Shape is RectangleTemplate { Width: 1, Length: 1 } && goal.Safe,
                "Pickup target not rotated-offset world-axis square.");
            var snap = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snap, new(new(0, 0, 5), 0, 6, false, false, 0, true, snap.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 1
                && goal.Shape.Resolve(goal.Position, 0, null).Contains(path[^1].Position), "Default solver did not reach pickup square.");
            f.Engine.DutyActions = [new(0, 9345, 1, 1)]; f.Render(); Check(!Group(f, "lakshmi/orb").Any(a => a.Navigate), "Charged action still collected orb.");
            f.Engine.DutyActions = [new(0, 9345, 0, 1)];
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 1290, Duration = 5, Timestamp = f.Now }, f.Now);
            f.Render(); Check(!Group(f, "lakshmi/orb").Any(a => a.Navigate), "Shield event not independent of charge.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 1290, Timestamp = f.Now }, f.Now);
            f.Render(); Check(Group(f, "lakshmi/orb").Any(a => a.Navigate), "Shield loss did not resume pickup.");
            f.PlayerId = 101; f.Scan(); Check(!Group(f, "lakshmi/orb").Any(a => a.Navigate), "Recorded local charges applied to another perspective.");
        });
        run("Lakshmi circle zone dead state7 remains obstacle no unsupported ban or target priority", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EA76C, new(10, 0, 0), Dead: true, EventState: 7); f.Scan();
            Check(Group(f, "lakshmi/zone").Single() is { Obstacle: true, Shape: CircleTemplate { Radius: 10 } } zone
                && zone.Position == new Vector3(10, 0, 0)
                && f.Snapshot.HardObstacles.Count == 1, "Dead/state7 object incorrectly filtered or no obstacle.");
            Check(f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.RequireAllStop && f.Engine.PriorityTarget(1, Vector3.Zero) == 0,
                "Zero enemy priority became unsupported ban/priority.");
            f.Actors.Remove(20); f.Scan(); Check(!Group(f, "lakshmi/zone").Any() && f.Snapshot.HardObstacles.Count == 0, "Removed zone leaked.");
        });
        run("Lakshmi Vril execution requests obey authorization identity charges cast death throttle and clear", () =>
        {
            var control = new LakshmiVrilControl(); var now = DateTime.UtcNow; var calls = new List<ulong>();
            var player = new ReplayActorState { Id = 100 };
            bool Submit(ulong id) { calls.Add(id); return true; }
            ReplayDutyAction[] charged = [new(0, 9345, 1, 1)];
            control.Update(true, false, player, 100, charged, now, Submit);
            control.Update(true, true, player, 101, charged, now, Submit);
            control.Update(true, true, player, 100, null, now, Submit);
            control.Update(true, true, player, 100, [new(0, 9345, 0, 1)], now, Submit);
            control.Update(true, true, player with { Dead = true }, 100, charged, now, Submit);
            control.Update(true, true, player with { Cast = new(1, 100, Vector3.Zero, 0, now, now.AddSeconds(3), false, false) },
                100, charged, now, Submit);
            Check(calls.Count == 0, "Execution leaked through a gate.");
            control.Update(true, true, player, 100, charged, now, Submit);
            control.Update(true, true, player, 100, charged, now.AddSeconds(.1), Submit);
            Check(calls.SequenceEqual([100UL]) && control.Attempts == 1, "First self cast/throttle differs.");
            control.Update(true, true, player, 100, charged, now.AddSeconds(.5), Submit);
            Check(calls.Count == 2, "Continued casting did not renew request.");
            control.Clear();
            control.Update(true, true, player, 100, charged, now, _ => false);
            control.Update(false, true, player, 100, charged, now.AddSeconds(.1), Submit);
            control.Update(false, true, player, 100, charged, now.AddSeconds(1), Submit);
            Check(calls.Count == 3 && control.Attempts == 1, "One-shot was lost before submission or submitted twice.");
            control.Clear(); Check(control.Attempts == 0, "Stop did not clear executor.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x1E20, 719);
        f.Actors[100] = new(100, 0, new(0, 0, 5), PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, new(10, 0, 0), PartySlot: 1, Hostile: false);
        f.Actors[102] = new(102, 0, new(-10, 0, 0), PartySlot: 2, Hostile: false);
        f.Actors[10] = new(10, 0x1E22, Vector3.Zero); f.Actors[11] = new(11, 0x1E22, Vector3.Zero);
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, ulong target)
    {
        var cast = new DungeonCastObservation(action, Vector3.Zero, 0, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
