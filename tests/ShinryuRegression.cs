using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class ShinryuRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Shinryu water independent flags shared fixed clock no timeout and real cast replacement", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1E950D, Vector3.Zero, EventState: 7); f.Scan();
            Check(Group(f, "shinryu/water").Single() is { Navigate: false, OutlineOnly: true }, "Idle puddle became danger.");
            Start(f, 10, 8091, Vector3.Zero, seconds: 20);
            Check(Group(f, "shinryu/water").Single() is { Navigate: true, Safe: false } water
                && water.Activation == f.Start.AddSeconds(6), "Lightning uses cast duration instead of fixed six.");
            f.Now = f.Start.AddSeconds(1); Start(f, 11, 8107, Vector3.Zero, seconds: 50);
            Check(Group(f, "shinryu/water").Single().Activation == f.Now.AddSeconds(10)
                && !Group(f, "shinryu/fire").Any(), "Coexisting flags lost shared clock/lightning precedence.");
            End(f, 10, 8091);
            var join = Group(f, "shinryu/fire/join").Single();
            var geometry = join.Shape.Resolve(join.Position, join.Rotation, null);
            Check(Group(f, "shinryu/water").Single() is { Safe: true, Navigate: false, Shape: CircleTemplate { Radius: 5 } }
                && !geometry.Contains(new(4.4f, 0, 0)) && geometry.Contains(new(4.6f, 0, 0)), "Fire 4.5 navigation/5 display differs.");
            f.Now = f.Start.AddSeconds(100); f.Scan();
            Check(Group(f, "shinryu/fire").Any(), "Predicted end cleared active fire.");
            Start(f, 11, 8732, Vector3.Zero); Check(!Group(f, "shinryu/fire").Any(), "Replacement did not finish old fire.");
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan();
            Check(!Group(f, "shinryu/water").Any(), "Dead puddle retained.");
        });
        run("Shinryu ice circles use effect caster position and finish position cancellation removes all matches", () =>
        {
            using var f = Scene();
            f.Actors[10] = f.Actors[10] with { Position = new(2, 0, 3) }; f.Scan();
            f.Engine.Effect(10, 8096, 1, f.Now, location: new(15, 0, 15));
            f.Engine.Effect(10, 8096, 2, f.Now); f.Render();
            var position = Group(f, "shinryu/ice").First().Position;
            Check(Group(f, "shinryu/ice").Length == 2 && Vector3.Distance(position, new(2, 0, 3)) < .04f,
                "Impact used event target location or collapsed independent effects.");
            Start(f, 11, 8097, Vector3.Zero);
            f.Actors[11] = f.Actors[11] with { Position = new(2, 5, 3) }; f.Scan();
            End(f, 11, 8097); Check(!Group(f, "shinryu/ice").Any(), "Finish did not clear every matching grid position.");
            f.Engine.Effect(10, 8096, 3, f.Now); Start(f, 10, 8097, new(10, 0, 10));
            f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Render();
            Check(!Group(f, "shinryu/ice").Any(), "Destroy skipped cast-finish circle cleanup.");
        });
        run("Shinryu three displayed thrusts independent three effective sequential knockbacks", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = new(0, 0, 1) };
            for (ulong i = 10; i < 15; ++i) f.Actors[i] = new(i, 0x1B16, Vector3.Zero);
            f.Scan();
            Start(f, 10, 8097, new(30, 0, 0));
            for (ulong i = 11; i < 15; ++i) Start(f, i, 8097, Vector3.Zero);
            Check(Group(f, "shinryu/thrust").Length == 3, "AOE maxCasts is not first three records.");
            var landings = Group(f, "shinryu/kb/8097").Where(a => a.Id.EndsWith("/landing")).ToArray();
            Check(landings.Length == 3 && landings[0].Id.Contains("/11/") && landings[1].Position.Z > landings[0].Position.Z,
                "KB capped before shape filtering or did not use previous landing.");
            Check(Group(f, "shinryu/kb/8097").All(a => !a.Navigate), "Thrust acquired unsupported KB navigation.");
            var before = Group(f, "shinryu/thrust").Length;
            f.Engine.Effect(10, 8097, 1, f.Now); f.Render();
            Check(Group(f, "shinryu/thrust").Length == before, "Effect replaced actual cast end.");
            End(f, 11, 8097); Check(!Group(f, "shinryu/thrust/11").Any() && Group(f, "shinryu/thrust").Length == 3, "First finish erased group or failed cap handoff.");
        });
        foreach (var (action, distance, radius) in new[] { (8075u, 35f, 12f), (8984u, 5f, 7f), (8080u, 15f, 7f) })
            run($"Shinryu knockback {action} exact distance first origin navigation and activation immunity", () =>
            {
                using var f = Scene();
                Start(f, 10, action, Vector3.Zero, rotation: MathF.PI / 2);
                var origin = Group(f, $"shinryu/kb/{action}/origin").Single();
                Check(!origin.Shape.Resolve(origin.Position, 0, null).Contains(new(radius - .1f, 0, 0)), "Origin radius differs.");
                var landing = Group(f, $"shinryu/kb/{action}/10/landing").Single();
                var expected = action == 8075 ? new Vector3(distance, 0, 5) : new Vector3(0, 0, 5 + distance);
                var path = Group(f, $"shinryu/kb/{action}/10/path").Single();
                Check(path.Shape is RectangleTemplate rectangle && MathF.Abs(rectangle.Length - distance) < .001f
                    && Vector3.Distance(path.Position * 2 - f.Actors[100].Position, expected) < .001f, "Actual movement length/endpoint differs.");
                Check(Vector3.Distance(landing.Position, expected) < .001f || landing.Unclipped, "Projection distance/direction differs.");
                if (action == 8075) Check(landing.Position.X > 19, "Tidal direction discarded packet rotation.");
                f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(3) } }; f.Scan();
                Check(Group(f, $"shinryu/kb/{action}").Any(), "Immunity ending before hit suppressed knockback.");
                f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 160, Duration = 30, Timestamp = f.Now }, f.Now); f.Render();
                Check(!Group(f, $"shinryu/kb/{action}").Any(), "Active immunity failed.");
            });
        run("Shinryu icon clocks independent target consumption death and EarthBreath FIFO", () =>
        {
            using var f = Scene();
            f.Engine.Icon(101, 24, f.Now, 100); f.Engine.Icon(100, 62, f.Now, 101);
            f.Engine.Icon(102, 23, f.Now, 101); f.Render();
            Check(Group(f, "shinryu/icon").Count(a => a.Display) == 3
                && MathF.Abs(Group(f, "shinryu/icon/1/display").Single().Position.Z - 5) < .04f
                && Group(f, "shinryu/icon/3/avoid").Single().Activation == f.Now.AddSeconds(5.1), "Icon actor/target identity or clock differs.");
            Check(f.Engine.PredictedDamage.Select(p => p.TargetId).Order().SequenceEqual(new ulong[] { 100, 102 }),
                "Spread/breath masks differ or default stack added unsupported prediction.");
            f.Engine.Effect(10, 8092, 1, f.Now, 100); f.Render();
            Check(!Group(f, "shinryu/icon/1").Any() && Group(f, "shinryu/icon/2").Any(), "Spread effect crossed stack.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            Check(!Group(f, "shinryu/icon/2").Any() && Group(f, "shinryu/icon/3").Any(), "Stack/bait death behavior differs.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(Group(f, "shinryu/icon/2").Any(), "Dead stack was deleted instead of hidden.");
            f.Engine.Effect(1, 8093, 2, f.Now, 100); f.Render();
            Check(!Group(f, "shinryu/icon/3").Any() && Group(f, "shinryu/icon/2").Any(), "Earth Breath effect crossed stack.");
            f.Engine.Effect(1, 8090, 3, f.Now, 999); f.Render(); Check(!Group(f, "shinryu/icon").Any(), "Fallback stack consumption failed.");
        });
        run("Shinryu tank-only stack target follows roles overwrite source finish ignores follow-up effects", () =>
        {
            using var f = Scene();
            Start(f, 10, 8100, Vector3.Zero, target: 100);
            Check(Group(f, "shinryu/tank/display").Single() is { OutlineOnly: true, Safe: false }
                && Group(f, "shinryu/tank/avoid").Length == 1 && f.Engine.PredictedDamage.Count() == 2, "Tank target rules differ.");
            f.PlayerId = 101; f.Scan();
            Check(Group(f, "shinryu/tank/position").Single().Shape is SohrKhaiWallHintTemplate, "Other tank not joining.");
            f.PlayerId = 102; f.Scan();
            Check(!Group(f, "shinryu/tank/display").Single().OutlineOnly
                && Group(f, "shinryu/tank/position").Single().Shape is CircleTemplate, "Non-tank not avoiding.");
            f.Engine.Effect(10, 8101, 1, f.Now); f.Render(); Check(Group(f, "shinryu/tank").Any(), "Follow-up effect cleared initial cast.");
            Start(f, 11, 8100, Vector3.Zero, target: 101); End(f, 10, 8100);
            Check(Group(f, "shinryu/tank").Any(), "Old source finish cleared replacement target.");
            End(f, 11, 8100); Check(!Group(f, "shinryu/tank").Any(), "Actual source finish leaked tank stack.");
        });
        run("Shinryu burning tether one player end fixed thirty no fake effect end and actual untether", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Tethers = new Dictionary<uint, ulong> { [9] = 101 } }; f.Scan();
            var avoid = Group(f, "shinryu/chain").Single(a => a.Navigate);
            Check(avoid.Shape is CircleTemplate { Radius: 30 } && avoid.Position == f.Actors[101].Position, "Tether distance/side differs.");
            f.Now = f.Start.AddSeconds(10); f.Scan(); f.Engine.Effect(10, 8144, 1, f.Now); f.Render();
            Check(Group(f, "shinryu/chain").Any(), "Tether timed out or hit consumed it.");
            f.PlayerId = 101; f.Scan(); Check(!Group(f, "shinryu/chain").Any(), "Single tether was made symmetric.");
            f.PlayerId = 100; f.Scan();
            f.Engine.Tether(101, 9, false, f.Now, 100); f.Render();
            Check(!Group(f, "shinryu/chain").Any(), "Untether leaked.");
        });
        run("Shinryu arena status loss platform integer round protected origin and bbox recenter", () =>
        {
            using var f = Scene();
            Check(!f.Engine.Arena!.Contains(new(25, 0, 0)), "Initial arena not half twenty.");
            f.Engine.StatusChanged(100, 667, true, f.Now); f.Render();
            Check(!f.Engine.Arena.Contains(new(25, 0, 0)), "Gain enlarged arena.");
            f.Engine.StatusChanged(100, 667, false, f.Now); f.Render();
            Check(f.Engine.Arena.Contains(new(29, 0, 29)), "Lose did not enlarge arena.");
            f.Actors[30] = new(30, 0x18D6, Vector3.Zero); f.Scan();
            f.Engine.Animation(30, 0x00040008, f.Now);
            for (ulong i = 31; i < 34; ++i)
            {
                f.Actors[i] = new(i, 0x1EA1A1, new(20.1f, 0, ((int)i - 32) * 20));
            }
            f.Actors[34] = new(34, 0x1EA1A1, new(.9f, 0, .9f)); f.Scan();
            f.Engine.Animation(34, 0x00400080, f.Now);
            foreach (var id in new[] { 31UL, 32UL, 33UL }) f.Engine.Animation(id, 0x00400080, f.Now);
            f.Render();
            Check(f.Engine.Arena.Contains(Vector3.Zero) && !f.Engine.Arena.Contains(new(20, 0, 0))
                && MathF.Abs(f.Engine.Arena.Position.X + 10) < .001f, "Broken tiles/protected center/bbox center differs.");
            Check(HostDrawingGeometry.Convert(f.Engine.Arena).Contains(Vector3.Zero), "World broken geometry lost center.");
            f.Engine.StatusChanged(100, 667, false, f.Now);
            Check(!f.Engine.Arena.Contains(new(20, 0, 0)), "Duplicate Lose restored broken tiles.");
            f.Actors.Remove(32); f.Scan(); Check(!f.Engine.Arena.Contains(new(20, 0, 0)), "Destroyed tile restored.");
        });
        run("Shinryu status loss stale raw acknowledgement and party destruction do not restore holes", () =>
        {
            using var f = Scene();
            f.Actors[101] = f.Actors[101] with { Statuses = [667] }; f.Scan();
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 101, StatusId = 667, Timestamp = f.Now }, f.Now);
            f.Now = f.Start.AddSeconds(10); f.Scan();
            Check(f.Engine.Arena!.Contains(new(29, 0, 0)), "Event lose lost to stale raw status.");
            f.Engine.Animation(10, 0x00040008, f.Now);
            f.Actors[30] = new(30, 0x1EA1A1, new(20, 0, 0)); f.Scan();
            f.Engine.Animation(30, 0x00400080, f.Now);
            f.Actors.Remove(101); f.Scan();
            Check(!f.Engine.Arena.Contains(new(20, 0, 0)), "Raw stale re-add caused destruction to re-enlarge arena.");
            f.Engine.RemoveActor(101); f.Render(); Check(!f.Engine.Arena.Contains(new(20, 0, 0)), "Late Remove restored holes.");
        });
        run("Shinryu thin ice exact thirty standard camera unknown no movement hold or artificial navigation", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Statuses = [911], Rotation = MathF.PI / 2 }; f.Scan();
            Check(Group(f, "shinryu/slide").Length == 3 && Group(f, "shinryu/slide").All(a => !a.Navigate)
                && !f.Engine.RequireAllStop && !f.Engine.RequireSirenMovementHold, "Thin ice changed controls/navigation.");
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("薄冰滑行将出界"), "Unsafe slide hint missing.");
            f.Engine.LegacyMovement = true; f.Engine.CameraAzimuth = null; f.Render();
            Check(Group(f, "shinryu/slide").Single().Id.EndsWith("/radius"), "Unknown replay camera guessed a slide or lost known radius.");
            f.Engine.CameraAzimuth = 0; f.Render();
            Check(Group(f, "shinryu/slide/landing").Single().Position.Z < -19, "Legacy camera direction discarded.");
        });
        run("Shinryu cast insertion order survives finished source reuse", () =>
        {
            using var f = Scene();
            for (ulong i = 10; i < 14; ++i) f.Actors[i] = new(i, 0x18D6, Vector3.Zero);
            f.Scan();
            for (ulong i = 10; i < 14; ++i) Start(f, i, 8097, Vector3.Zero);
            End(f, 10, 8097);
            Start(f, 10, 8097, Vector3.Zero);
            var shown = Group(f, "shinryu/thrust");
            Check(shown.Length == 3 && shown.All(a => !a.Id.StartsWith("shinryu/thrust/10", StringComparison.Ordinal)),
                "Source reuse overtook pending thrusts.");
            var paths = Group(f, "shinryu/kb/8097").Where(a => a.Id.EndsWith("/path")).ToArray();
            Check(paths.Length == 3 && paths[0].Id.Contains("/11/") && paths[2].Id.Contains("/13/"),
                "Knockback FIFO differs after source reuse.");
        });
        run("Shinryu tether packets remain authoritative until raw acknowledgement", () =>
        {
            using var f = Scene();
            f.Engine.Tether(101, 9, true, f.Now, 100); f.Render();
            f.Now = f.Start.AddSeconds(2); f.Scan();
            Check(Group(f, "shinryu/chain").Any(), "Stale empty raw erased pending tether.");
            f.Actors[100] = f.Actors[100] with { Tethers = new Dictionary<uint, ulong> { [9] = 101 } }; f.Scan();
            f.Engine.Tether(101, 9, false, f.Now, 100); f.Render();
            f.Now = f.Start.AddSeconds(4); f.Scan();
            Check(!Group(f, "shinryu/chain").Any(), "Stale raw revived untether.");
            f.Actors[100] = f.Actors[100] with { Tethers = null }; f.Scan();
            f.Actors[100] = f.Actors[100] with { Tethers = new Dictionary<uint, ulong> { [9] = 102 } }; f.Scan();
            Check(Group(f, "shinryu/chain").Single(a => a.Navigate).Position == f.Actors[102].Position,
                "Acknowledged untether blocked later raw connection.");
        });
        run("Shinryu actual status slots survive predicted expiry and packet slide removal", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Statuses = [], StatusExpirations = new Dictionary<uint, DateTime>
                { [667] = f.Start.AddSeconds(-1), [911] = f.Start.AddSeconds(-1) } }; f.Scan();
            f.Now = f.Start.AddSeconds(10); f.Scan();
            Check(!f.Engine.Arena!.Contains(new(29, 0, 0)) && Group(f, "shinryu/slide").Any(),
                "Estimated status expiry replaced actual status removal.");
            f.Engine.StatusChanged(100, 911, false, f.Now); f.Render();
            f.Now = f.Start.AddSeconds(12); f.Scan();
            Check(!Group(f, "shinryu/slide").Any(), "Stale slide slot revived removed status.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = null }; f.Scan();
            Check(f.Engine.Arena.Contains(new(29, 0, 0)), "Actual fetter disappearance failed to enlarge arena.");
        });
        run("Shinryu spread navigation includes extra yard and shared earliest prediction", () =>
        {
            using var f = Scene();
            f.Engine.Icon(101, 24, f.Now); f.Render();
            var avoid = Group(f, "shinryu/icon").Single(a => a.Navigate);
            Check(avoid.Shape.Resolve(avoid.Position, avoid.Rotation, null).Contains(f.Actors[101].Position + new Vector3(5.5f, 0, 0)),
                "Spread navigation lost extra yard.");
            f.Now = f.Start.AddSeconds(2); f.Engine.Icon(100, 24, f.Now); f.Render();
            Check(f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(6)),
                "Spread predictions did not share earliest activation.");
            Check(!Group(f, "shinryu/icon").Any(a => a.Id.EndsWith("/buddy/101", StringComparison.Ordinal)),
                "Other spread target treated as unmarked partner.");
        });
        run("Shinryu destroyed actors cannot retain pending slide or tether after id reuse", () =>
        {
            using var f = Scene();
            f.Engine.StatusChanged(101, 911, true, f.Now);
            f.Engine.Tether(102, 9, true, f.Now, 101);
            f.Engine.RemoveActor(101); f.Actors.Remove(101); f.Scan();
            f.Actors[101] = new(101, 0, new(10, 0, 0), PartySlot: 1, Hostile: false,
                Tethers: new Dictionary<uint, ulong> { [9] = 100 });
            f.PlayerId = 101; f.Scan();
            Check(!Group(f, "shinryu/slide").Any() && Group(f, "shinryu/chain").Any(),
                "Pending control survived actor destruction and blocked reused id.");
        });
        run("Shinryu late dead spread icon cannot advance living damage prediction", () =>
        {
            using var f = Scene();
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Engine.Icon(101, 24, f.Now);
            f.Now = f.Start.AddSeconds(2); f.Engine.Icon(102, 24, f.Now); f.Render();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(8),
                "Dead spread advanced active prediction.");
        });
        run("Shinryu native ownership exact cast shapes proximity predictions source scope and dragon priority", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.Create(679).Any(d => d.BossOid == 0x1983)
                && !ArrDungeonCatalog.Create(674).Any(d => d.BossOid == 0x1983), "Ownership crossed territory.");
            foreach (var (action, front, half) in new[] { (8730u, 15f, 5.5f), (8180u, 100f, 30f), (8083u, 40f, 10f), (9130u, 40f, 10f), (9146u, 20f, 10f) })
            {
                Start(f, 10, action, Vector3.Zero);
                var shape = f.Draw.Previews["cast/10"].Geometry;
                Check(shape.Contains(new(half - .1f, 0, front - .1f))
                    && HostDrawingGeometry.Convert(shape).Contains(new(half - .1f, 0, front - .1f))
                    && !shape.Contains(new(half + .1f, 0, front - .1f)), "Exact rectangle dual-view differs: " + action);
                End(f, 10, action);
            }
            Start(f, 10, 9291, Vector3.Zero);
            Check(f.Engine.PredictedDamage.Count() == 3 && f.Draw.Previews["cast/10"].Geometry.Contains(new(13.9f, 0, 0))
                && !f.Draw.Previews["cast/10"].Geometry.Contains(new(14.1f, 0, 0)), "Proximity radius/prediction differs.");
            f.Actors[20] = new(20, 0x1B14, Vector3.UnitX * 10); f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 20 && f.Engine.ForbiddenTargets.Length == 0, "Dragon priority or unsupported shield ban differs.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Now = f.Start.AddSeconds(10001); f.Scan();
            Check(f.Engine.Active != null, "Hidden primary or state display length ended DeathPhase.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x1983, 679);
        f.Actors[100] = new(100, 0, new(0, 0, 5), PartySlot: 0, Hostile: false, PartyRole: "Tank", ClassJob: 19);
        f.Actors[101] = new(101, 0, new(10, 0, 0), PartySlot: 1, Hostile: false, PartyRole: "Tank", ClassJob: 21);
        f.Actors[102] = new(102, 0, new(-10, 0, 0), PartySlot: 2, Hostile: false, PartyRole: "Healer", ClassJob: 24);
        f.Actors[10] = new(10, 0x18D6, Vector3.Zero);
        f.Actors[11] = new(11, 0x18D6, Vector3.Zero);
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, Vector3 location, float rotation = 0, ulong target = 0, double seconds = 5)
    {
        var cast = new DungeonCastObservation(action, location, rotation, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
