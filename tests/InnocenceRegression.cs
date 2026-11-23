using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class InnocenceRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Innocence rotating waves retain float callback timing and only earliest eight plus original cast", () =>
        {
            using var f = Scene();
            Start(f, 10, 16049, location: new(110, 0, 100), rotation: .2f);
            var waves = Group(f, "wave");
            Check(waves.Length == 8 && waves[0].Activation == f.Start.AddSeconds((double)2.7f)
                && waves[1].Activation == f.Start.AddSeconds((double)(2.7f + .6f))
                && waves[1].Shape is PtDonutSectorTemplate { Inner: 15, Outer: 20 }
                && MathF.Abs(waves[1].Rotation - (.2f + 8f * MathF.PI / 180)) < .00001f
                && Vector3.Distance(waves[1].Position, new(109.903f, 0, 98.608f)) < .04f
                && f.Draw.Previews.ContainsKey("cast/10"), "Rotating geometry/clock or simultaneous long cast differs.");
            var resolved = waves[0].Shape.Resolve(waves[0].Position, waves[0].Rotation, null);
            Check(HostDrawingGeometry.Convert(resolved).Contains(new(113.378f, 0, 116.661f)), "World sector conversion differs.");
        });
        run("Innocence wave queue survives time and ignores alternate instant until actual watched resolution", () =>
        {
            using var f = Scene(); Start(f, 10, 16049);
            f.Now = f.Start.AddSeconds(5); f.Render();
            Check(Group(f, "wave").Length == 8 && Group(f, "wave")[0].Activation == f.Start.AddSeconds((double)2.7f),
                "Clock removed unresolved waves.");
            f.Engine.Effect(10, 16122, 1, f.Now); f.Render();
            Check(Group(f, "wave")[0].Activation == f.Start.AddSeconds((double)2.7f), "Wrong instant resolved predictions.");
            f.Engine.Effect(10, 16121, 2, f.Now); f.Render();
            Check(Group(f, "wave").Length == 8 && Group(f, "wave")[0].Activation == f.Start.AddSeconds((double)(2.7f + .6f * 4)),
                "Watched instant did not remove all elapsed entries.");
            f.Now = f.Start.AddSeconds(100); End(f, 10, 16049);
            Check(!Group(f, "wave").Any(), "Actual finish failed to drain elapsed queue.");
        });
        run("Innocence interleaved long casts sort one shared prediction queue and finish all due entries", () =>
        {
            using var f = Scene(); Start(f, 10, 16049);
            f.Now = f.Start.AddSeconds(.1); Start(f, 11, 16050, rotation: 1);
            var waves = Group(f, "wave");
            Check(waves.Length == 8 && waves[1].Activation == f.Now.AddSeconds((double)2.7f) && waves[1].Rotation == 1,
                "Interleaved lists were limited separately or not sorted.");
            f.Now = f.Start.AddSeconds(3); End(f, 10, 16049);
            Check(Group(f, "wave").All(a => a.Activation > f.Now) && f.Draw.Previews.ContainsKey("cast/11"),
                "Finish removed only its caster or erased other original cast.");
        });
        run("Innocence wave clear requires centered nail destruction and not death or near-center destruction", () =>
        {
            using var f = Scene(); Start(f, 10, 16049);
            f.Actors[20] = new(20, 0x2900, new(100, 0, 100), Dead: true); f.Scan();
            Check(Group(f, "wave").Length == 8, "Death incorrectly cleared the sequence.");
            f.Actors[21] = new(21, 0x2900, new(100.001f, 0, 100)); f.Scan();
            f.Engine.RemoveActor(21); f.Actors.Remove(21); f.Scan();
            Check(Group(f, "wave").Length == 8, "Near-center matched exact-position cleanup.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); f.Scan();
            Check(!Group(f, "wave").Any(), "Centered destruction did not clear waves.");
        });
        run("Innocence corrected long cast finishes due queue before appending another twenty six", () =>
        {
            using var f = Scene(); Start(f, 10, 16049);
            f.Now = f.Start.AddSeconds(4);
            var correction = f.Actors[10].Cast! with { Rotation = 1 };
            f.Engine.StartCast(10, correction, f.Now); f.Render();
            Check(Group(f, "wave").Length == 8 && Group(f, "wave").All(a => a.Activation > f.Now),
                "Corrected packet left elapsed old waves.");
        });
        run("Innocence meteor towers keep raw birth positions and clear all on any tower destruction", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EAD40, new(85.123f, 2, 100));
            f.Actors[21] = new(21, 0x1EAD40, new(115, 0, 100)); f.Scan();
            Check(Group(f, "tower").Count(a => a.Display) == 2 && Group(f, "tower").Any(a => a.Position.X == 85.123f)
                && Group(f, "tower/join").Single().Activation == null, "Tower position/activation differs.");
            f.Engine.Effect(10, 16023, 1, f.Now); f.Render();
            Check(Group(f, "tower").Count(a => a.Display) == 2, "Sinsphere hit cleared towers early.");
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan();
            Check(Group(f, "tower").Count(a => a.Display) == 2, "Tower death was mistaken for destruction.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); f.Scan();
            Check(!Group(f, "tower").Any(), "Any tower destruction did not clear whole set.");
        });
        run("Innocence tower occupancy capacity outsider join and damage exclude corpses", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EAD40, f.Actors[101].Position); f.Scan();
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("进入罪球塔")
                && f.Engine.PredictedDamage.Single() is { TargetId: 101, Activation: var at } && at == default,
                "One-soaker outsider hint/default-time damage differs.");
            f.Actors[100] = f.Actors[100] with { Position = f.Actors[101].Position }; f.Scan();
            Check(Group(f, "tower").Single(a => a.Display).Safe, "Two people cannot share tower.");
            f.Actors[102] = f.Actors[102] with { Position = f.Actors[101].Position }; f.Scan();
            Check(Group(f, "tower").Single(a => a.Display) is { Safe: false, OutlineOnly: true }, "Own overfull styling differs.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            Check(f.Engine.PredictedDamage.Count() == 2, "Corpse counted as tower damage.");
        });
        run("Innocence default-range gaze uses locked position PR facing and dual-view eye", () =>
        {
            using var f = Scene(); Start(f, 1, 16025);
            f.Actors[1] = f.Actors[1] with { Position = new(100, 0, 115) }; f.Scan();
            Check(Group(f, "gaze").Single(a => a.Id.EndsWith("/eye")).Position.Z == 100 && f.Engine.Gazes.Single().Location.Z == 100,
                "Gaze eye followed live caster rather than locked spell position.");
            Check(f.Engine.GazeFacing(f.Actors[100].Position, -MathF.PI / 2) == null, "Facing applied before control window.");
            f.Now = f.Start.AddSeconds(5);
            f.Engine.Tick(f.Now);
            Check(f.Engine.GazeFacing(new(500, 0, 100), -MathF.PI / 2) is { } rotation
                && MathF.Abs(MathF.Atan2(MathF.Sin(rotation + MathF.PI / 2), MathF.Cos(rotation + MathF.PI / 2))) > MathF.PI / 4,
                "Default-range gaze failed PR-facing output.");
            End(f, 1, 16025); Check(!Group(f, "gaze").Any(), "Finished gaze leaked.");
        });
        run("Innocence righteous bolt dynamic dead target defaults raidwide and ends with cast source", () =>
        {
            using var f = Scene(); Start(f, 10, 16035, target: 101);
            f.Actors[101] = f.Actors[101] with { Position = new(113, 0, 100), Dead = true }; f.Scan();
            Check(Group(f, "bait/16035").Single(a => a.Display).Position.X == 113
                && f.Engine.PredictedDamage.Single() is { TargetId: 101, DamageType: "Raidwide" }, "Dead-target bait omitted.");
            f.Engine.Effect(10, 16035, 1, f.Now, 101); f.Render();
            Check(Group(f, "bait/16035").Any(), "Effect ended cast bait.");
            End(f, 10, 16035); Check(!Group(f, "bait/16035").Any(), "True cast finish retained bait.");
        });
        run("Innocence drop icon resolves targetID FIFO and dead target removal is permanent", () =>
        {
            using var f = Scene();
            f.Engine.Icon(100, 138, f.Now, 101); f.Engine.Icon(101, 138, f.Now); f.Render();
            Check(f.Engine.PredictedDamage.Count() == 2 && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(5.1)),
                "Icon targetID or delay differs.");
            f.Engine.Effect(10, 16069, 1, f.Now, 101); f.Render();
            Check(f.Engine.PredictedDamage.Single().TargetId == 101, "Resolve consumed matching target rather than FIFO.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(!Group(f, "bait/16069").Any(), "Revived icon target restored deleted bait.");
        });
        run("Innocence line stack marker clock dynamic source dead target and unresolved final target", () =>
        {
            using var f = Scene(); f.Engine.Effect(10, 14588, 1, f.Now, 101); f.Render();
            var line = Group(f, "line").Single(a => a.Display);
            Check(line.Shape is RayRectangleTemplate { Length: 40, MinLocalX: -3, MaxLocalX: 3 }
                && Group(f, "line/join").Single().Activation == f.Start.AddSeconds(4.7)
                && f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 3, "Line clock/width/prediction differs.");
            f.Actors[101] = f.Actors[101] with { Position = new(110, 0, 100), Dead = true }; f.Scan();
            Check(Group(f, "line").Single(a => a.Display).Rotation > 1.5f, "Line stopped tracking dead marked target.");
            f.Engine.Effect(1, 16070, 2, f.Now, 999); f.Render();
            Check(Group(f, "line").Any(), "Unresolved final target deleted line.");
            f.Engine.Effect(1, 16070, 3, f.Now, 102); f.Render();
            Check(!Group(f, "line").Any(), "Valid single-line final-target correction did not consume line.");
        });
        run("Innocence multiple line stack markers resolve independently and marked role gets buddy navigation", () =>
        {
            using var f = Scene();
            f.Engine.Effect(10, 14588, 1, f.Now, 100); f.Engine.Effect(11, 14588, 2, f.Now, 101); f.Render();
            Check(Group(f, "line").Any(a => a.Id.EndsWith("/buddy")) && Group(f, "line").Any(a => a.Id.EndsWith("/avoid")),
                "Marked-player line buddy/other-line navigation missing.");
            f.Engine.Effect(1, 16070, 3, f.Now, 101); f.Render();
            Check(Group(f, "line").Count(a => a.Display) == 1, "One line resolve erased the other.");
        });
        run("Innocence source phase-two overload huge circle exact line widths and independent finishes", () =>
        {
            using var f = Scene(); Start(f, 10, 18066);
            Check(f.Draw.Previews["cast/10"].Geometry.Contains(new(500, 0, 100)), "Source radius overload was changed.");
            End(f, 10, 18066); Start(f, 10, 16065);
            Check(!f.Draw.Previews.ContainsKey("cast/10"), "Unwatched flaming sword created a cast AoE.");
            Start(f, 10, 16051); Start(f, 11, 16071);
            Check(f.Draw.Previews["cast/10"].Geometry.Contains(new(101.9f, 0, 139))
                && !f.Draw.Previews["cast/10"].Geometry.Contains(new(102.1f, 0, 139))
                && f.Draw.Previews["cast/11"].Geometry.Contains(new(114.9f, 0, 144))
                && !f.Draw.Previews["cast/11"].Geometry.Contains(new(115.1f, 0, 144)), "Full-width rects differ.");
            End(f, 10, 16051); Check(f.Draw.Previews.ContainsKey("cast/11"), "Independent finish lost other AoE.");
        });
        run("Innocence line damage uses earliest active source and warns marked player in another line", () =>
        {
            using var f = Scene();
            f.Engine.Effect(10, 14588, 1, f.Now, 101);
            f.Now = f.Start.AddSeconds(1); f.Engine.Effect(11, 14588, 2, f.Now, 100); f.Render();
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(1).AddSeconds(4.7)),
                "Inactive first line supplied predicted damage clock.");
            f.Actors[11] = f.Actors[11] with { Dead = true }; f.Scan();
            Check(!f.Engine.PredictedDamage.Any(), "Dead sources retained shared damage.");
            f.Actors[10] = f.Actors[10] with { Dead = false };
            f.Actors[11] = f.Actors[11] with { Dead = false };
            f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 105) }; f.Scan();
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("远离其他光柱分摊"), "Marked player in another stack lacked warning.");
        });
        run("Innocence raidwide cast-only gates all five IDs and potential targets remain zero priority", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 16027u, 16030u, 16114u, 16019u, 16106u })
            { Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 3, "Raidwide cast missing."); End(f, 10, action); }
            Start(f, 10, 16026); Check(!f.Engine.PredictedDamage.Any(), "Unwatched Realmrazer invented damage.");
            f.Actors[20] = new(20, 0x28FC, new(100, 0, 100)); f.Scan();
            Check(f.Engine.PriorityTarget(1, f.Actors[100].Position) == 0 && f.Engine.ForbiddenTargets.Length == 0,
                "Zero priority or no-source shield contract changed.");
        });
        run("Innocence phase one remains primary while phase two alive and phase-two-only death ends outputs", () =>
        {
            using var f = Scene(); f.Actors[2] = new(2, 0x28FE, new(100, 0, 100), InCombat: true); f.Scan();
            f.Engine.Icon(101, 138, f.Now); f.Render();
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan(); Start(f, 10, 16049);
            Check(f.Engine.Active != null && f.Engine.PrimaryId == 1 && f.Engine.PreserveStageAcrossBattleEnd && Group(f, "wave").Any()
                && !Group(f, "bait/16069").Any(), "Phase handoff replaced primary or ended all output.");
            f.Engine.RemoveActor(1); f.Actors.Remove(1); f.Scan();
            Check(f.Engine.Active != null && f.Engine.PrimaryId == 1, "Destroyed P1 ended live P2.");
            f.Actors[2] = f.Actors[2] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final death leaked outputs.");
        });
        run("Innocence absent phase two uses vacuous all-dead and hidden primary alone does not end", () =>
        {
            using var f = Scene(); f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(f.Engine.Active != null && ArrDungeonCatalog.IsScript("masterplayer.trial.innocence"), "Hidden primary or owner gate differs.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null, "Missing P2 forced a nonexistent phase.");
        });
    }
    private static Fight Scene()
    {
        var f = new Fight(0x28FB, 846); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 5, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false);
        f.Actors[102] = new(102, 0, c - Vector3.UnitX * 10, PartySlot: 2, Hostile: false);
        f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c); f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0, Vector3? location = null, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now);
        f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes()
        .Where(a => a.Id.StartsWith("innocence/" + prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
