using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Heavensward.Dungeon.D06AetherochemicalResearchFacility;

internal static class ResearchTrashRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (native, type, field) in new (IGeometryShape, Type, string)[]
        {
            (ResearchTrashArenas.Enforcement(0), typeof(Source.D060Trash1.D060Trash1), "arena"),
            (ResearchTrashArenas.Vanguard(0), typeof(Source.D060SixthLegionMagitekVanguardH1.D060SixthLegionMagitekVanguardH1), "arena"),
            (ResearchTrashArenas.Dreadnaught(0), typeof(Source.D060FacilityDreadnaught.D060FacilityDreadnaught), "arena"),
            (ResearchTrashArenas.Shabti(0), typeof(Source.D060CulturedShabti.D060CulturedShabti), "arena1"),
            (ResearchTrashArenas.Conjurer(0), typeof(Source.D060ClonedConjurer.D060ClonedConjurer), "arena1"),
            (ResearchTrashArenas.ShabtiConjurer(0), typeof(Source.ShabtiConjurer2Bounds), "Arena"),
            (ResearchTrashArenas.BiocultureFirst(0), typeof(Source.D060BiocultureNode.D060BiocultureNode), "Arena1"),
            (ResearchTrashArenas.BiocultureFirstFilled(0), typeof(Source.D060BiocultureNode.D060BiocultureNode), "Arena1b"),
            (ResearchTrashArenas.BiocultureSecond(0), typeof(Source.D060BiocultureNode.D060BiocultureNode), "arena2"),
            (ResearchTrashArenas.BiocultureSecondFilled(0), typeof(Source.D060BiocultureNode.D060BiocultureNode), "Arena2b"),
        })
            run($"ResearchFacility trash arena {field} {type.Name} preserves holes and both views", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(native);
                var b = native.GetBounds();
                for (var x = b.MinX + .137f; x <= b.MaxX; x += .53f)
                    for (var z = b.MinZ + .231f; z <= b.MaxZ; z += .53f)
                    {
                        var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(new(x, 0, z)) == expected && host.Contains(new(x, 0, z)) == expected,
                            $"Arena mismatch at {x}, {z}.");
                    }
            });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0xFA3u, 0xFA3u, 33030u, 16f), (0xFA5u, 0xFA4u, 609u, 7.5f),
            (0x1350u, 0x1350u, 526u, 8.8f), (0x1350u, 0xF4Bu, 866u, 3.5f),
            (0xF54u, 0xF54u, 4556u, 11f), (0xF54u, 0xF55u, 4825u, 42.4f), (0xF54u, 0xF54u, 4557u, 8f),
            (0xF58u, 0xF58u, 1259u, 8.2f), (0xF58u, 0xF58u, 1260u, 22.2f),
            (0xF4Du, 0xF50u, 4465u, 5.5f), (0xF4Du, 0xF4Fu, 4703u, 7f),
            (0xF4Du, 0x10B0u, 4666u, 6.9f), (0xF4Du, 0xF50u, 4464u, 7.5f),
            (0xF4Du, 0x10B3u, 3342u, 9.7f), (0xF4Du, 0x10B3u, 3341u, 9.7f),
            (0xF4Du, 0x10B3u, 3340u, 9.7f), (0xF4Du, 0x10B3u, 3343u, 9.7f),
            (0xF4Du, 0xF51u, 4519u, 9f), (0xF4Du, 0x10B2u, 917u, 3f),
        })
            run($"ResearchFacility trash AID {action} preserves geometry time and cancellation", () =>
            {
                using var f = new Fight(boss, 1110);
                var source = boss == caster ? 1UL : 10UL;
                if (source != 1) f.Actors[source] = new(source, caster, Vector3.Zero);
                var player = boss == 0xF4D ? new Vector3(28, 0, 210) : Vector3.Zero;
                f.Engine.Synchronize(f.Actors.Values, player, f.Now, playerId: 100);
                f.Cast(source, action, 3, MathF.PI / 2);
                var geometry = f.Draw.Previews.Values.Single().Geometry;
                Check(geometry.Contains(Vector3.UnitX * (range - .02f)) && !geometry.Contains(Vector3.UnitX * (range + .02f)),
                    "Wrong shape extent.");
                Check(HostDrawingGeometry.Convert(geometry).Contains(Vector3.UnitX * (range - .02f)), "World shape differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "Wrong activation.");
                f.Engine.Finish(source); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Cancel leaked.");
            });
        run("ResearchFacility trash pull on another group member and survive primary removal", () =>
        {
            using var f = new Fight(0xFA5, 1110);
            f.Engine.ResetFight();
            f.Actors[1] = f.Actors[1] with { InCombat = false };
            f.Actors[10] = new(10, 0xFA4, Vector3.Zero, InCombat: true); f.Scan();
            Check(f.Engine.Active?.BossOid == 0xFA5, "Another member did not activate the pull.");
            f.Cast(10, 609, 3);
            f.Engine.RemoveActor(1); f.Actors.Remove(1); f.Scan();
            Check(f.Engine.Active != null && f.Draw.Previews.Count == 1, "Primary removal cleared a surviving group's cast.");
            f.Actors.Remove(10); f.Scan();
            Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null && f.Draw.Previews.Count == 0, "All-dead group leaked.");
        });
        run("ResearchFacility infrared follows target and headspin uses actual viewer job", () =>
        {
            using var f = new Fight(0xFA5, 1110);
            f.Actors[1] = f.Actors[1] with { TargetId = 101 };
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, ClassJob: 24, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 9, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0xF5A, Vector3.UnitZ * 8, TargetId: 101); f.Scan();
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2
                && f.Draw.Previews["research/cleave/1/display"].Geometry.Contains(Vector3.UnitX * 9)
                && !f.Draw.Previews["research/cleave/1/display"].Geometry.Contains(Vector3.Zero), "Infrared origin or ranged headspin differs.");
            foreach (var job in new uint[] { 19, 20, 30, 39, 41 })
            {
                f.Actors[100] = f.Actors[100] with { ClassJob = job }; f.Scan();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Tank/melee retained headspin.");
            }
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Any(d => d.Geometry.Contains(Vector3.Zero)), "Target did not avoid cleaving a teammate.");
        });
        foreach (var first in new[] { true, false })
            run($"ResearchFacility bioculture {(first ? "first" : "second")} activates by entry and closes only its hole", () =>
            {
                using var f = new Fight(0xF4D, 1110);
                var node = first ? new Vector3(28, 0, 220) : new Vector3(112, 0, 272);
                var inside = node - Vector3.UnitZ * 10;
                f.Actors[1] = f.Actors[1] with { Position = node, InCombat = false };
                f.Engine.Synchronize(f.Actors.Values, Vector3.Zero, f.Now);
                Check(f.Engine.Active == null, "Inactive room activated from outside.");
                f.Engine.Synchronize(f.Actors.Values, inside, f.Now); f.Render();
                Check(f.Engine.Active?.BossOid == 0xF4D && !f.Engine.Arena!.Contains(node), "Entry pull or initial hole differs.");
                f.Actors[10] = new(10, first ? 0x1E9934u : 0x1E9935u, node);
                f.Engine.Synchronize(f.Actors.Values, inside, f.Now);
                f.Engine.Animation(10, 0x00040008); f.Render();
                Check(f.Engine.Arena!.Contains(node), "Animation did not close hole.");
                f.Engine.ResetFight();
                f.Engine.Synchronize(f.Actors.Values, inside, f.Now); f.Render();
                Check(f.Engine.Arena!.Contains(node), "Pull restart forgot scene hole closure.");
            });
        run("ResearchFacility shared Shabti Conjurer area retains both concurrent mechanisms", () =>
        {
            using var f = new Fight(0xF58, 1110);
            f.Actors[10] = new(10, 0xF56, Vector3.Zero, InCombat: true);
            f.Actors[100] = new(100, 0, Vector3.UnitX * 10, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.Zero, PartySlot: 1, Hostile: false); f.Scan();
            f.Cast(1, 1259, 3);
            f.Engine.StartCast(10, new(900, Vector3.Zero, 0, f.Now, f.Now.AddSeconds(5.3), 101), f.Now); f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2, "Concurrent source module was dropped.");
            f.Actors.Remove(1); f.Engine.RemoveActor(1); f.Render();
            Check(f.Engine.Active != null && f.Draw.Previews.Count == 1, "Primary death removed surviving Tornado.");
            Check(!f.Engine.Gazes.Any(), "Nonactivated Breakga became a gaze.");
        });
        run("ResearchFacility Vanguard names interruptible Stoneskin caster", () =>
        {
            using var f = new Fight(0x1350, 1110);
            f.Actors[10] = new(10, 0xF4C, Vector3.Zero, Name: "六军团医护兵"); f.Scan(); f.Cast(10, 316, 2.5);
            Check(f.Engine.Hints(Vector3.Zero).Any(h => h.Contains("六军团医护兵") && h.Contains("眩晕")), "Caster hint missing.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
