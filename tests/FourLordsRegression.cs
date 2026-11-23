using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class FourLordsRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("FourLords Byakko simple casts keep independent true endings full fan and exact ray widths", () =>
        {
            using var f = Scene(false);
            Start(f, 10, 10798, Vector3.Zero); Start(f, 11, 10819, Vector3.Zero);
            Check(f.Draw.Previews.Count == 2 && f.Draw.Previews["cast/10"].Geometry.Contains(Vector3.UnitX * 20)
                && !f.Draw.Previews["cast/10"].Geometry.Contains(-Vector3.UnitZ * 20), "Sweep full270 geometry differs.");
            var rect = f.Draw.Previews["cast/11"].Geometry;
            Check(rect.Contains(new(2.4f, 0, 44.7f)) && !rect.Contains(new(2.6f, 0, 44.7f))
                && HostDrawingGeometry.Convert(rect).Contains(new(2.4f, 0, 44.7f)), "ImperialGuard ray differs.");
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(f.Draw.Previews.Count == 2, "Predicted end cleared active cast.");
            End(f, 10, 10798); Check(f.Draw.Previews.Count == 1, "Unrelated true finish cleared second caster.");
            End(f, 11, 10819); Check(f.Draw.Previews.Count == 0, "True finish leaked.");
        });
        run("FourLords Byakko mixed FireAndLightning coexist and preserve separate source resolution", () =>
        {
            using var f = Scene(false);
            Start(f, 10, 10796, Vector3.Zero); Start(f, 11, 10801, Vector3.UnitX);
            Check(f.Draw.Previews.Count == 2, "Mixed group lost one action.");
            End(f, 11, 10801); Check(f.Draw.Previews.ContainsKey("cast/10"), "Reverse finish consumed wrong cast.");
        });
        run("FourLords Byakko heavenly strike has independent single target prediction and spread radii", () =>
        {
            using var f = Scene(false);
            Start(f, 10, 10797, Vector3.Zero, target: 101);
            Check(f.Engine.PredictedDamage.Single().TargetId == 101
                && Group(f, "byakko/spread").Single(a => a.Display).Shape is CircleTemplate { Radius: 3 }
                && Group(f, "byakko/spread").Single(a => a.Navigate).Shape is CircleTemplate { Radius: 4 },
                "Tankbuster/spread component parity failed.");
            f.Actors[101] = f.Actors[101] with { Position = new(12, 0, 0) }; f.Scan();
            Check(Group(f, "byakko/spread").First().Position.X > 11.9f, "Spread did not follow player.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            Check(!Group(f, "byakko/spread").Any(), "Dead spread target remained visible.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(Group(f, "byakko/spread").Any(), "Revival did not restore cast-based spread before actual finish.");
        });
        run("FourLords Byakko moving orb renders capsule and four rolling prediction windows", () =>
        {
            using var f = Scene(false);
            f.Actors[20] = new(20, 0x20F9, Vector3.Zero, Rotation: MathF.PI / 2, Targetable: false); f.Scan();
            var aoes = Group(f, "byakko/orb");
            Check(aoes.Length == 6 && aoes.Single(a => a.Display).Shape is DungeonCapsuleTemplate { Radius: 2, Length: 2 }
                && aoes.Single(a => a.Obstacle).Shape is CircleTemplate { Radius: 2 }
                && aoes.Count(a => a.Navigate && !a.Obstacle) == 4
                && aoes.Any(a => a.Activation == DateTime.MaxValue), "Orb geometry/rolling windows differ.");
            var first = aoes.Single(a => a.Id.EndsWith("/1")).Activation;
            f.Now = f.Start.AddSeconds(1); f.Actors[20] = f.Actors[20] with { Position = new(3, 0, 0) }; f.Scan();
            Check(Group(f, "byakko/orb").Single(a => a.Id.EndsWith("/1")).Activation == first!.Value.AddSeconds(1),
                "Orb prediction anchored at birth.");
            f.Actors[100] = f.Actors[100] with { Position = new(3, 0, 0) }; f.Scan();
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("远离荒魂移动范围"), "Orb contact hint missing.");
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan(); Check(!Group(f, "byakko/orb").Any(), "Dead orb persisted.");
        });
        run("FourLords Byakko inactive components and impossible source donut do not invent output", () =>
        {
            using var f = Scene(false);
            Start(f, 10, 10800, Vector3.Zero);
            f.Engine.Icon(100, 62, f.Now); f.Engine.Icon(100, 101, f.Now); f.Engine.Effect(10, 10808, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && !f.Engine.AmbientAoes().Any(), "Inactive source component was activated.");
        });
        run("FourLords Suzaku ScarletFever changes arena only at actual finish and keeps central hole", () =>
        {
            using var f = Scene(true);
            Start(f, 10, 12844, new(100, 0, 100));
            Check(f.Engine.Arena!.Contains(new(100, 0, 100)), "Ring applied at start.");
            f.Engine.Effect(10, 12844, 1, f.Now); f.Render();
            Check(f.Engine.Arena!.Contains(new(100, 0, 100)), "Ring applied at effect.");
            End(f, 10, 12844);
            Check(!f.Engine.Arena!.Contains(new(100, 0, 100)) && f.Engine.Arena.Contains(new(105, 0, 100))
                && !f.Engine.Arena.Contains(new(121, 0, 100))
                && f.Engine.Arena.GetContours().Count == 2, "Ring boundary or inner contour missing.");
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(!f.Engine.Arena.Contains(new(100, 0, 100)), "Observe reverted ring.");
        });
        run("FourLords Suzaku eight yard knockback honors immunity shows unsafe landing and adds no source AI", () =>
        {
            using var f = Scene(true);
            f.Actors[100] = f.Actors[100] with { Position = new(118, 0, 100) }; f.Scan();
            Start(f, 10, 12848, new(100, 0, 100));
            var end = Group(f, "suzaku/knockback").Single(a => a.Id.EndsWith("/landing"));
            Check(MathF.Abs(end.Position.X - 120) < .01f && end.Unclipped && !end.Navigate
                && f.Engine.Hints(f.Actors[100].Position).Contains("击退落点将出界"), "KB stopped at wall or lost risk.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160],
                StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(10) } }; f.Scan();
            Check(!Group(f, "suzaku/knockback").Any(), "Immunity was ignored.");
        });
        run("FourLords Suzaku simple cones rectangles true finish and unsupported effect do not create tank cast", () =>
        {
            using var f = Scene(true); var c = new Vector3(100, 0, 100);
            Start(f, 10, 12851, c); Start(f, 11, 12854, c);
            Check(f.Draw.Previews.Count == 2 && f.Draw.Previews["cast/10"].Geometry.Contains(c + Vector3.UnitX * 20),
                "PhantomFlurry full180 missing.");
            End(f, 10, 12851); End(f, 11, 12854);
            f.Engine.Effect(10, 12850, 1, f.Now, 100); f.Render();
            Check(!f.Engine.PredictedDamage.Any() && f.Draw.Previews.Count == 0, "No-cast effect invented source cast prediction.");
            foreach (var action in new[] { 12833u, 12852u, 12831u, 12844u })
            {
                Start(f, 10, action, c); Check(f.Engine.PredictedDamage.Count() == 2, "Raidwide omitted."); End(f, 10, action);
            }
        });
        run("FourLords destroyed cast sources resolve spread and ScarletFever state", () =>
        {
            using var byakko = Scene(false);
            Start(byakko, 10, 10797, Vector3.Zero, target: 101);
            byakko.Engine.RemoveActor(10); byakko.Actors.Remove(10); byakko.Scan();
            Check(!Group(byakko, "byakko/spread").Any() && !byakko.Engine.PredictedDamage.Any(),
                "Destroyed source retained a cast component.");
            using var suzaku = Scene(true);
            Start(suzaku, 10, 12844, new(100, 0, 100));
            suzaku.Engine.RemoveActor(10); suzaku.Actors.Remove(10); suzaku.Scan();
            Check(!suzaku.Engine.Arena!.Contains(new(100, 0, 100)),
                "Source destruction did not dispatch source cast finish.");
            var contours = suzaku.Engine.Arena.GetContours();
            Check(contours[0].Count == 46 && contours[1].Count == 20,
                "Parsed source ring was replaced by a different polygon approximation.");
        });
        run("FourLords native ownership hidden trivial phase and death cleanup", () =>
        {
            foreach (var suzaku in new[] { false, true })
            {
                using var f = Scene(suzaku);
                Check(ArrDungeonCatalog.IsScript(suzaku ? "masterplayer.trial.suzaku" : "masterplayer.trial.byakko")
                    && f.Engine.ForbiddenTargets.Length == 0, "Ownership or invented shield restriction.");
                Start(f, 10, suzaku ? 12851u : 10798u, f.Actors[1].Position);
                f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Now = f.Start.AddSeconds(10001); f.Scan();
                Check(f.Engine.Active != null, "Hidden trivial phase ended.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
                Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Death leaked output.");
            }
        });
    }
    private static Fight Scene(bool suzaku)
    {
        var f = new Fight(suzaku ? 0x2459u : 0x20F7u, suzaku ? 810u : 746u);
        var c = suzaku ? new Vector3(100, 0, 100) : Vector3.Zero;
        f.Actors[1] = f.Actors[1] with { Position = c };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 5, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c - Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
        f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c); f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, Vector3 position, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, position, 0, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
