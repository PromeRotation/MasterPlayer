using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class SusanoRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (action, length, width) in new[] { (8222u, 6f, 0f), (9631u, 65f, 10f), (8588u, 65f, 6f),
            (8259u, 6f, 0f), (8232u, 41f, 40f), (8233u, 27f, 40f), (8234u, 27f, 40f), (8235u, 27f, 40f) })
            run($"Susano {action} exact geometry dual views independent real cast ending", () =>
            {
                using var f = new Fight(0x1AF7, 674);
                f.Actors[10] = new(10, 0x1BA1, Vector3.Zero); f.Scan(); f.Cast(10, action, 4);
                var preview = f.Draw.Previews.Single(a => a.Key.StartsWith("cast/")).Value.Geometry;
                var world = HostDrawingGeometry.Convert(preview);
                Check(preview.Contains(new(0, 0, length - .01f)) && world.Contains(new(0, 0, length - .01f)), "Length/radius differs.");
                if (width > 0) Check(!preview.Contains(new(width / 2 + .01f, 0, 1)) && !preview.Contains(new(0, 0, -.01f)), "Rect width/back differs.");
                var window = f.Snapshot.DangerAreas.Single().DangerWindows.Single();
                Check(window.DangerFrom == f.Start.AddSeconds(4.3) && window.DangerUntil == null, "Native clock/lifetime differs.");
                f.Now = f.Start.AddSeconds(40); f.Scan(); Check(f.Engine.Casts.Count == 1, "Timeout cleared active cast.");
                f.Engine.Effect(10, action, 1, f.Now); f.Render(); Check(f.Engine.Casts.Count == 1, "Effect replaced real finish.");
                f.Actors[10] = f.Actors[10] with { Cast = null }; f.Scan();
                Check(f.Snapshot.DangerAreas.Count == 0 && !f.Draw.Previews.Keys.Any(k => k.StartsWith("cast/")), "Finish leaked cast.");
            });
        run("Susano simultaneous sea splitters cancel only matching source and no inferred effect sequence", () =>
        {
            using var f = new Fight(0x1AF7, 674);
            for (ulong id = 10; id < 14; ++id) f.Actors[id] = new(id, 0x1BA1, Vector3.Zero);
            f.Scan();
            for (ulong id = 10; id < 14; ++id) f.Cast(id, (uint)(8232 + id - 10), 5);
            f.Engine.Finish(11, 8233); f.Actors[11] = f.Actors[11] with { Cast = null }; f.Render();
            Check(f.Engine.Casts.Count == 3 && f.Engine.Casts.ContainsKey(10) && f.Engine.Casts.ContainsKey(12), "Cancellation crossed sources.");
            f.Engine.ResetFight(); f.Scan();
            foreach (var id in f.Actors.Keys.ToArray()) f.Actors[id] = f.Actors[id] with { Cast = null };
            f.Engine.ResetFight(); f.Scan();
            f.Engine.Effect(10, 8232, 1, f.Now); f.Engine.Icon(100, 23); f.Render();
            Check(f.Engine.Casts.Count == 0 && !Group(f, "susano/knockback").Any(), "Unimplemented icon/effect inferred fake stages.");
        });
        run("Susano icon actor clocks min2 unbounded capacity dynamic target death and FIFO effects", () =>
        {
            using var f = new Fight(0x1AF7, 674);
            Party(f);
            f.Engine.Icon(101, 62, f.Now, 100); f.Render();
            var stack = Group(f, "susano/stack/1/display").Single();
            Check(stack.Position.X < 0 && stack.Activation == null && stack.Safe, "Icon actor identity/quantization differs.");
            Check(Group(f, "susano/stack/1/buddy").Single().Activation == f.Start.AddSeconds(8), "Eight seconds became eight-person capacity.");
            Check(!f.Engine.PredictedDamage.Any(), "Source empty-forbidden-mask branch should not add stack damage.");
            f.Actors[101] = f.Actors[101] with { Position = Vector3.Zero }; f.Scan();
            Check(!f.Engine.Hints(Vector3.Zero).Contains("至少两人分摊"), "Two players did not satisfy min2.");
            f.Engine.Icon(102, 62, f.Start.AddSeconds(1)); f.Render();
            Check(Group(f, "susano/stack/2/display").Single().Safe == false, "Other marked stack should be danger outline.");
            Check(!Group(f, "susano/stack").Any(a => a.Id.EndsWith("/avoid", StringComparison.Ordinal)),
                "Unlimited capacity generated contradictory join/avoid.");
            f.Actors[102] = f.Actors[102] with { Position = Vector3.Zero }; f.Scan();
            Check(f.Engine.Hints(Vector3.Zero).Contains("至少两人分摊"), "Overlapping marked players lost warning.");
            f.Actors[100] = f.Actors[100] with { Position = Vector3.UnitZ * 5, Dead = true }; f.Scan();
            Check(!Group(f, "susano/stack/1/display").Any() && Group(f, "susano/stack/2/display").Any(), "Dead marked target outputs differ.");
            f.Actors[100] = f.Actors[100] with { Dead = false }; f.Scan();
            Check(Group(f, "susano/stack/1/display").Single().Position.Z > 4.9, "Revival lost retained icon.");
            f.Now = f.Start.AddSeconds(50); f.Scan(); Check(Group(f, "susano/stack").Count(a => a.Display) == 2, "Timer consumed stack.");
            f.Engine.Effect(1, 8224, 1, f.Now, 102); f.Render();
            Check(Group(f, "susano/stack/1/display").Any() && !Group(f, "susano/stack/2/display").Any(), "Matched effect consumed first stack.");
            f.Engine.Effect(1, 8224, 2, f.Now, 999); f.Render();
            Check(!Group(f, "susano/stack").Any(), "Unmatched effect failed FIFO fallback.");
        });
        run("Susano shared line tankbuster dynamic heading separate stack effect and perspective", () =>
        {
            using var f = new Fight(0x1AF7, 674); Party(f);
            f.Actors[101] = f.Actors[101] with { Position = Vector3.UnitZ * 10 }; f.Scan();
            f.Engine.Icon(102, 230, f.Now, 101); f.Render();
            Check(Group(f, "susano/tank/display").Single() is { Safe: true, OutlineOnly: true }
                && Group(f, "susano/tank/position").Single().Activation == f.Start.AddSeconds(5.1), "Other tank join/clock differs.");
            Check(f.Engine.PredictedDamage.Count(p => p.DamageType == "Tankbuster") == 2, "Both tank predictions missing.");
            f.PlayerId = 101; f.Scan(); Check(!Group(f, "susano/tank/position").Any(), "Marked rectangular tank got unsupported bait navigation.");
            f.PlayerId = 102; f.Actors[101] = f.Actors[101] with { Position = Vector3.UnitX * 10 }; f.Scan();
            var line = Group(f, "susano/tank/display").Single();
            Check(!line.OutlineOnly && !line.Safe && MathF.Abs(line.Rotation - MathF.PI / 2) < .001, "Non-tank fill/dynamic target differs.");
            f.Engine.Effect(1, 8224, 1, f.Now); f.Render(); Check(Group(f, "susano/tank").Any(), "Stack effect cleared TB.");
            f.Engine.Effect(1, 8227, 2, f.Now); f.Render(); Check(!Group(f, "susano/tank").Any(), "TB effect leaked.");
        });
        run("Susano cast knockback radial unclamped immunities independent finish and orb display only", () =>
        {
            using var f = new Fight(0x1AF7, 674); Party(f);
            f.Actors[100] = f.Actors[100] with { Position = Vector3.UnitZ * 5 };
            f.Actors[10] = new(10, 0x1B9B, Vector3.UnitX); f.Scan(); f.Cast(1, 8223, 4);
            var landing = Group(f, "susano/knockback/1/landing").Single();
            Check(landing.Position.Z == 20 && landing.Unclipped && !landing.Navigate
                && f.Engine.Hints(Vector3.Zero).Contains("击退落点将出界"), "Outside projection marker lost boundary/unsupported navigation.");
            Check(Group(f, "susano/orb").Single() is { Navigate: false, Shape: CircleTemplate { Radius: 1 }, OutlineOnly: false }, "Orb became hard danger or soak goal.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 160, Duration = 5, Timestamp = f.Now }, f.Now);
            f.Render(); Check(!Group(f, "susano/knockback").Any(), "Immunity event not used.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 160, Timestamp = f.Now }, f.Now);
            f.Render(); Check(Group(f, "susano/knockback").Any(), "Removal did not clear immunity.");
            f.Engine.Finish(1, 8223); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Render();
            Check(!Group(f, "susano/knockback").Any() && Group(f, "susano/orb").Any(), "Finish crossed orb lifetime.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(!Group(f, "susano/orb").Any(), "Dead orb still drawn.");
            Check(!Group(f, "susano/enemy/10").Any(), "Dead auxiliary enemy still drawn.");
        });
        run("Susano hidden primary stays active source death resets no instant raidwide false positives", () =>
        {
            using var f = new Fight(0x1AF7, 674); Party(f);
            f.Actors[1] = f.Actors[1] with { Targetable = false, InCombat = false }; f.Scan();
            Check(f.Engine.Active != null && f.Engine.PreserveStageAcrossBattleEnd, "Sword phase hidden primary reset.");
            f.Engine.Effect(1, 8225, 1, f.Now); f.Engine.Effect(1, 8226, 2, f.Now);
            Check(!f.Engine.PredictedDamage.Any() && f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.RequireAllStop, "Unsupported instant timing/ban added.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.ArenaBoundary == null, "DeathPhase did not clean.");
        });
    }

    private static void Party(Fight f)
    {
        f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, PartyRole: "Tank", Hostile: false);
        f.Actors[101] = new(101, 0, Vector3.UnitX * 10, PartySlot: 1, PartyRole: "Tank", Hostile: false);
        f.Actors[102] = new(102, 0, Vector3.UnitZ * 10, PartySlot: 2, PartyRole: "Healer", Hostile: false); f.Scan();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
