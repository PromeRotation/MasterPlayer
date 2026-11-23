using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class ResearchFacilityRegression
{
    internal static void Run(Action<string, Action> run)
    {
        ResearchStarsRegression.Run(run);
        ResearchTrashRegression.Run(run);
        run("ResearchFacility native ownership isolates migrated bosses", () =>
        {
            Check(ArrDungeonCatalog.Owns(0xE97, 1110) && ArrDungeonCatalog.Owns(0xE9A, 1110)
                && !ArrDungeonCatalog.Owns(0xE97, 0), "Wrong ownership.");
        });
        foreach (var (native, source) in new[]
        {
            (ResearchFacilityArenas.Regula(0), BossMod.Heavensward.Dungeon.D06AetherochemicalResearchFacility.D061Regula.D061Regula.arena),
            (ResearchFacilityArenas.Harmachis(0), BossMod.Heavensward.Dungeon.D06AetherochemicalResearchFacility.D062Harmachis.D062Harmachis.arena),
            (ResearchFacilityArenas.Lahabrea(0), BossMod.Heavensward.Dungeon.D06AetherochemicalResearchFacility.D063LahabreaIgeyorhm.D063LahabreaIgeyorhm.arena),
            (ResearchFacilityArenas.AscianPrime(0), BossMod.Heavensward.Dungeon.D06AetherochemicalResearchFacility.D064AscianPrime.D064AscianPrime.arena),
        })
            run($"ResearchFacility arena {native.Position} preserves source and world boundaries", () =>
            {
                var host = HostDrawingGeometry.Convert(native);
                for (var x = -25.17f; x < 25; x += .37f)
                    for (var z = -25.23f; z < 25; z += .37f)
                    {
                        var point = native.Position + new Vector3(x, 0, z);
                        var expected = source.Contains(new(point.X - source.Center.X, point.Z - source.Center.Z));
                        Check(native.Contains(point) == expected && host.Contains(point) == expected, "Arena differs.");
                    }
            });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0xE97u, 0xE99u, 4322u, 8f), (0xE97u, 0xE98u, 4321u, 50f),
            (0xE97u, 0x1B2u, 4318u, 8f), (0xE97u, 0xE97u, 4315u, 61.65f),
            (0xE97u, 0xE97u, 4316u, 31.65f), (0xE9Au, 0xE9Au, 4326u, 45f),
            (0xE9Au, 0x1B2u, 4325u, 5f),
            (0x3DA4u, 0x3DA4u, 32790u, 40f), (0x3DA7u, 0x233Cu, 31909u, 5f),
            (0x3DA7u, 0x233Cu, 31923u, 40f), (0x3DA7u, 0x233Cu, 31915u, 41f),
        })
            run($"ResearchFacility AID {action} exact extent deadline cancellation and both views", () =>
            {
                using var f = new Fight(boss, 1110); var source = boss == caster ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Vector3.Zero); f.Scan(); }
                f.Cast(source, action, 3, MathF.PI / 2);
                var shape = f.Draw.Previews.Values.Single().Geometry;
                Check(shape.Contains(Vector3.UnitX * (range - .02f)) && !shape.Contains(Vector3.UnitX * (range + .02f))
                    && HostDrawingGeometry.Convert(shape).Contains(Vector3.UnitX * (range - .02f)), "Display geometry differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "Wrong deadline.");
                f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancel leaked.");
            });
        run("ResearchFacility laser tether keeps packet state through stale scan and removes on effect", () =>
        {
            using var f = new Fight(0xE97, 1110);
            f.Actors[10] = new(10, 0xE98, Vector3.Zero);
            f.Actors[100] = new(100, 0, Vector3.UnitZ * 8, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false); f.Scan();
            f.Engine.Tether(100, 17, true, f.Start, 10); f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Tether lost in stale snapshot.");
            f.Engine.Effect(10, 4321, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Effect leaked laser.");
        });
        run("ResearchFacility laser restores a reverse player tether snapshot without resurrecting after cancel", () =>
        {
            using var f = new Fight(0xE97, 1110);
            f.Actors[10] = new(10, 0xE98, Vector3.Zero);
            f.Actors[100] = new(100, 0, Vector3.UnitZ * 8, PartySlot: 0, Hostile: false, Tethers: new Dictionary<uint, ulong> { [17] = 10 });
            f.Scan(); Check(f.Draw.Previews.Count == 1, "Reverse tether snapshot was ignored.");
            f.Engine.Tether(100, 17, false, f.Now, 10); f.Now = f.Start.AddSeconds(.6); f.Scan();
            Check(f.Draw.Previews.Count == 0, "Stale reverse tether resurrected cancelled laser.");
        });
        run("ResearchFacility gaze uses MP PR facing window and cancels cleanly", () =>
        {
            using var f = new Fight(0xE9A, 1110); f.Cast(1, 4331, 3);
            var player = Vector3.UnitZ * -10;
            Check(f.Engine.GazeFacing(player, 0) == null, "Facing starts too early.");
            f.Now = f.Start.AddSeconds(2.6); f.Engine.Tick(f.Now);
            var facing = f.Engine.GazeFacing(player, 0);
            Check(facing != null && Math.Abs(facing.Value) > MathF.PI / 4, "Gaze did not request safe PR facing.");
            f.Engine.Finish(1); f.Render();
            Check(f.Engine.GazeFacing(player, 0) == null && f.Draw.Previews.Count == 0, "Facing cancel leaked.");
        });
        foreach (var (action, count, delay) in new[] { (4335u, 2, 6.2), (4336u, 4, 4.1) })
            run($"ResearchFacility {count} person stack keeps event clock and suppresses cleave", () =>
            {
                using var f = new Fight(0xE9A, 1110);
                f.Actors[1] = f.Actors[1] with { TargetId = 100 };
                f.Actors[100] = new(100, 0, Vector3.UnitX * 10, PartySlot: 0, Hostile: false);
                f.Actors[101] = new(101, 0, Vector3.Zero, PartySlot: 1, Hostile: false); f.Scan();
                Check(f.Engine.AmbientAoes().Any(a => a.Id == "cleave/display"), "Cleave missing before stack.");
                f.Now = f.Start.AddSeconds(.3);
                if (action == 4335)
                    f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 101, StatusId = 2518, Timestamp = f.Start }, f.Now);
                else f.Engine.Icon(101, 93, f.Start);
                f.Render();
                Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("cleave/")) && f.Snapshot.GoalAreas.Count == 1
                    && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(delay)), "Stack timing, goal or cleave differs.");
                f.Engine.Effect(1, action, 1, f.Now); f.Render();
                Check(f.Snapshot.GoalAreas.Count == 0 && f.Engine.AmbientAoes().Any(a => a.Id == "cleave/display"), "Stack resolution failed.");
            });
        run("ResearchFacility two person stack rejects a third soaker and binds never resurrect after hit", () =>
        {
            using var f = new Fight(0xE9A, 1110);
            f.Actors[100] = new(100, 0, Vector3.UnitX * 10, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.Zero, PartySlot: 1, Hostile: false, Statuses: [2518]);
            f.Actors[102] = new(102, 0, Vector3.Zero, PartySlot: 2, Hostile: false); f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == 1, "Third player allowed in two stack.");
            f.Engine.Effect(1, 4335, 1, f.Now); f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Bind snapshot revived resolved stack.");
        });
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
